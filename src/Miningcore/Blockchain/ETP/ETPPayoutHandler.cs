using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Autofac;
using AutoMapper;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Payments;
using Miningcore.Persistence;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Repositories;
using Miningcore.Rpc;
using Miningcore.Time;
using Miningcore.Util;
using Newtonsoft.Json;
using NLog;
using Block = Miningcore.Persistence.Model.Block;
using Contract = Miningcore.Contracts.Contract;
using static Miningcore.Util.ActionUtils;

namespace Miningcore.Blockchain.ETP
{
    [CoinFamily(CoinFamily.ETP)]
    public class ETPPayoutHandler : PayoutHandlerBase,
        IPayoutHandler
    {
        public ETPPayoutHandler(
            IComponentContext ctx,
            IConnectionFactory cf,
            IMapper mapper,
            IShareRepository shareRepo,
            IBlockRepository blockRepo,
            IBalanceRepository balanceRepo,
            IPaymentRepository paymentRepo,
            IMasterClock clock,
            IMessageBus messageBus) :
            base(cf, mapper, shareRepo, blockRepo, balanceRepo, paymentRepo, clock, messageBus)
        {
            Contract.RequiresNonNull(ctx);
            Contract.RequiresNonNull(balanceRepo);
            Contract.RequiresNonNull(paymentRepo);

            this.ctx = ctx;
        }

        private readonly IComponentContext ctx;
        private RpcClient rpcClient;
        private ETPPayoutHandlerConfig config;
        private new ILogger logger;

        protected override string LogCategory => "ETP Payout Handler";

        #region IPayoutHandler

        public virtual async Task ConfigureAsync(ClusterConfig cc, PoolConfig pc, CancellationToken ct)
        {
            Contract.RequiresNonNull(cc);
            Contract.RequiresNonNull(pc);

            logger = LogManager.GetLogger(LogCategory);
            poolConfig = pc;

            // extract standard daemon config
            var jsonSerializerSettings = ctx.Resolve<JsonSerializerSettings>();

            var daemonEndpoints = pc.Daemons
                .Where(x => string.IsNullOrEmpty(x.Category))
                .ToArray();

            rpcClient = new RpcClient(daemonEndpoints.First(), jsonSerializerSettings, messageBus, pc.Id);

            config = pc.PaymentProcessing.Extra.SafeExtensionDataAs<ETPPayoutHandlerConfig>();

            await Task.CompletedTask;
        }

        public async Task<Block[]> ClassifyBlocksAsync(IMiningPool pool, Block[] blocks, CancellationToken ct)
        {
            Contract.RequiresNonNull(poolConfig);
            Contract.RequiresNonNull(blocks);

            var pageSize = 100;
            var pageCount = (int) Math.Ceiling(blocks.Length / (double) pageSize);
            var result = new List<Block>();

            for(var i = 0; i < pageCount; i++)
            {
                var page = blocks
                    .Skip(i * pageSize)
                    .Take(pageSize)
                    .ToArray();

                foreach(var block in page)
                {
                    var blockInfo = await rpcClient.ExecuteAsync<DaemonResponses.GetBlockResponse>(logger,
                        "getblock", ct, new[] { block.BlockHeight.ToString() });

                    if(blockInfo.Error == null && blockInfo.Response?.Block != null)
                    {
                        if(blockInfo.Response.Block.Hash == block.TransactionConfirmationData)
                        {
                            block.Status = BlockStatus.Confirmed;
                            block.ConfirmationProgress = 1;
                            result.Add(block);
                        }
                        else
                        {
                            block.Status = BlockStatus.Orphaned;
                            block.ConfirmationProgress = 0;
                            result.Add(block);
                        }
                    }
                    else
                        logger.Warn($"[{LogCategory}] Unable to classify block {block.BlockHeight}: {blockInfo.Error?.Message} Code {blockInfo.Error?.Code}");
                }
            }

            return result.ToArray();
        }

        public override async Task<decimal> UpdateBlockRewardBalancesAsync(IDbConnection con, IDbTransaction tx,
            IMiningPool pool, Block block, CancellationToken ct)
        {
            var blockRewardRemaining = await base.UpdateBlockRewardBalancesAsync(con, tx, pool, block, ct);

            // Ensure we have enough funds in the pool wallet to cover the payout
            var infoResponse = await rpcClient.ExecuteAsync<DaemonResponses.GetBalanceResponse>(logger,
                "getbalance", ct);

            if(infoResponse.Error != null)
                throw new Exception($"[{LogCategory}] Error checking pool wallet balance: {infoResponse.Error.Message}");

            var walletBalance = decimal.Parse(infoResponse.Response.Balance);

            if(walletBalance < blockRewardRemaining)
                throw new Exception($"[{LogCategory}] Insufficient pool wallet balance {walletBalance} to cover block reward {blockRewardRemaining}");

            return blockRewardRemaining;
        }

        public async Task PayoutAsync(IMiningPool pool, Balance[] balances, CancellationToken ct)
        {
            Contract.RequiresNonNull(balances);

            if(balances.Length == 0)
                return;

            try
            {
                logger.Info(() => $"[{LogCategory}] Paying out {balances.Length} balances");

                var txHashes = await Guard(() => PayoutBatch(balances, ct), ex => 
                    logger.Error(() => $"[{LogCategory}] Failed to pay out {balances.Length} balances: {ex.Message}"));

                if(txHashes != null && txHashes.Any())
                {
                    await PersistPaymentsAsync(balances, string.Join(", ", txHashes));
                    var txFee = CalculateTxFee(txHashes);
                    NotifyPayoutSuccess(pool.Config.Id, balances, txHashes.ToArray(), txFee);
                }
                else
                    NotifyPayoutFailure(pool.Config.Id, balances, $"No txHashes returned from PayoutBatch", null);
            }

            catch(Exception ex)
            {
                logger.Error(() => $"[{LogCategory}] Failed to pay out {balances.Length} balances: {ex.Message}");
                NotifyPayoutFailure(pool.Config.Id, balances, ex.Message, ex);
            }
        }

        public double AdjustBlockEffort(double effort)
        {
            return effort;
        }

        #endregion // IPayoutHandler

        private async Task<string[]> PayoutBatch(Balance[] balances, CancellationToken ct)
        {
            var txHashes = await Task.WhenAll(balances.Select(async balance =>
            {
                logger.Info(() => $"[{LogCategory}] Paying out {FormatAmount(balance.Amount)} to {balance.Address}");

                var response = await rpcClient.ExecuteAsync<string>(logger, "send", ct, new[]
                {
                    balance.Address,
                    balance.Amount.ToString("0.00000000")
                });

                if(response.Error == null)
                    return response.Response;

                throw new Exception($"[{LogCategory}] Daemon command 'send' returned error: {response.Error.Message} code {response.Error.Code}");
            }));

            return txHashes;
        }

        private decimal CalculateTxFee(string[] txHashes)
        {
            // implement txFee calculation logic here
            return 0;
        }
    }
}
