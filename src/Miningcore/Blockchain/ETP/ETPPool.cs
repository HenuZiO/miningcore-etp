using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;
using Autofac;
using AutoMapper;
using Microsoft.IO;
using Miningcore.Configuration;
using Miningcore.JsonRpc;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Nicehash;
using Miningcore.Notifications.Messages;
using Miningcore.Persistence;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Repositories;
using Miningcore.Stratum;
using Miningcore.Time;
using Newtonsoft.Json;
using Contract = Miningcore.Contracts.Contract;
using static Miningcore.Util.ActionUtils;
using Miningcore.Blockchain.ETP.Configuration;

namespace Miningcore.Blockchain.ETP
{
    [CoinFamily(CoinFamily.ETP)]
    public class ETPPool : PoolBase
    {
        public ETPPool(IComponentContext ctx,
            JsonSerializerSettings serializerSettings,
            IConnectionFactory cf,
            IStatsRepository statsRepo,
            IMapper mapper,
            IMasterClock clock,
            IMessageBus messageBus,
            RecyclableMemoryStreamManager rmsm,
            NicehashService nicehashService) :
            base(ctx, serializerSettings, cf, statsRepo, mapper, clock, messageBus, rmsm, nicehashService)
        {
            // Initialize subjects
            shareSubject = new Subject<Share>();
            validSharesSubject = new Subject<Unit>();
            invalidSharesSubject = new Subject<Unit>();
        }

        private ETPJobManager manager;
        private readonly ISubject<Share> shareSubject;
        private readonly ISubject<Unit> validSharesSubject;
        private readonly ISubject<Unit> invalidSharesSubject;

        private void OnShare(Share share)
        {
            // Store share for statistics
            shareSubject.OnNext(share);

            // Monitor submission rate
            if (share.NetworkDifficulty > 0) // Успешная шара
            {
                validSharesSubject.OnNext(Unit.Default);
            }
            else
            {
                invalidSharesSubject.OnNext(Unit.Default);
            }
        }

        protected override async Task OnRequestAsync(StratumConnection connection,
            Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
        {
            var request = tsRequest.Value;
            var context = connection.ContextAs<ETPWorkerContext>();

            try
            {
                switch(request.Method)
                {
                    case ETPConstants.StratumMethods.Subscribe:
                        await OnSubscribeAsync(connection, request);
                        break;

                    case ETPConstants.StratumMethods.Authorize:
                        await OnAuthorizeAsync(connection, request);
                        break;

                    case ETPConstants.StratumMethods.Submit:
                        await OnSubmitAsync(connection, request);
                        break;

                    default:
                        logger.Debug(() => $"[{connection.ConnectionId}] Unsupported RPC request: {JsonConvert.SerializeObject(request, serializerSettings)}");

                        await connection.RespondErrorAsync(StratumError.Other, $"Unsupported request {request.Method}", request.Id);
                        break;
                }
            }
            catch (StratumException ex)
            {
                await connection.RespondErrorAsync(ex.Code, ex.Message, request.Id, false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"[{connection.ConnectionId}] Error processing request {request.Method}: {ex.Message}");
                await connection.RespondErrorAsync(StratumError.Other, ex.Message, request.Id, false);
            }
        }

        protected override void OnConnect(StratumConnection connection, IPEndPoint portItem1)
        {
            var context = new ETPWorkerContext();
            connection.SetContext(context);

            logger.Info(() => $"[{connection.ConnectionId}] Client connected from {portItem1.Address}");
        }

        protected override async Task SetupJobManager(CancellationToken ct)
        {
            manager = ctx.Resolve<ETPJobManager>();

            manager.Configure(poolConfig, clusterConfig);

            await manager.StartAsync(ct);

            if (poolConfig.EnableInternalStratum == true)
            {
                logger.Info(() => "Setting up stratum job notifications...");

                disposables.Add(manager.Jobs.Subscribe(job =>
                {
                    logger.Debug(() => $"Broadcasting job to {connections.Count} workers");

                    // Send job to connected workers
                    ForEachMinerAsync(async (client, _) =>
                    {
                        try 
                        {
                            var context = client.ContextAs<ETPWorkerContext>();
                            await client.NotifyAsync(ETPConstants.StratumMethods.SetDifficulty, new object[] { context.Difficulty });
                            await client.NotifyAsync(ETPConstants.StratumMethods.MiningNotify, job.GetJobParamsForStratum());
                            logger.Debug(() => $"Sent job to worker {client.ConnectionId}");
                        }
                        catch (Exception ex)
                        {
                            logger.Error(() => $"Failed to send job to worker {client.ConnectionId}: {ex.Message}");
                        }
                    });
                }));

                // Monitor stratum connection events
                disposables.Add(manager.Jobs.Subscribe(job => 
                {
                    logger.Info(() => $"New job received at height {job.Height}");
                }));

                logger.Info(() => "Stratum job notifications setup complete");
            }
        }

        protected override async Task InitStatsAsync(CancellationToken ct)
        {
            await base.InitStatsAsync(ct);
        }

        protected override WorkerContextBase CreateWorkerContext()
        {
            return new ETPWorkerContext();
        }

        protected override async Task OnVarDiffUpdateAsync(StratumConnection connection, double newDiff, CancellationToken ct)
        {
            await base.OnVarDiffUpdateAsync(connection, newDiff, ct);

            if (connection.Context != null)
            {
                var context = connection.ContextAs<ETPWorkerContext>();
                context.Difficulty = newDiff;

                await connection.NotifyAsync(ETPConstants.StratumMethods.SetDifficulty, new object[] { newDiff });
            }
        }

        public override double HashrateFromShares(double shares, double interval)
        {
            var multiplier = Math.Pow(2, 32);
            var result = shares * multiplier / interval;
            return result;
        }

        public override async Task RunAsync(CancellationToken ct)
        {
            logger.Info(() => "Starting Pool ...");

            try
            {
                await base.RunAsync(ct);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        private void LogPoolInfo()
        {
            logger.Info(() => $"Pool: {poolConfig.Id} [{string.Join(", ", poolConfig.Ports.Keys)}]");
            logger.Info(() => $"Network: {poolConfig.Coin}");
        }

        private async Task OnSubscribeAsync(StratumConnection connection, JsonRpcRequest request)
        {
            logger.Info($"[{connection.ConnectionId}] New miner connected");
            manager.PrepareWorker(connection);
            await connection.RespondAsync(true, request.Id);
        }

        private async Task OnAuthorizeAsync(StratumConnection connection, JsonRpcRequest request)
        {
            var context = connection.ContextAs<ETPWorkerContext>();
            var authParams = request.ParamsAs<string[]>();
            var workerValue = authParams?.Length > 0 ? authParams[0] : null;

            if (!string.IsNullOrEmpty(workerValue))
            {
                // Split worker from wallet if format is wallet.worker
                var split = workerValue.Split('.');
                context.Worker = split.Length > 1 ? split[1] : split[0];
                context.Miner = split[0];
                logger.Info($"[{connection.ConnectionId}] Authorized miner {context.Worker} (Wallet: {context.Miner})");
            }

            // Initialize worker
            manager.PrepareWorker(connection);

            // Send successful response with worker name
            await connection.RespondAsync(true, request.Id);
        }

        private async Task OnSubmitAsync(StratumConnection connection, JsonRpcRequest request)
        {
            if (request.Id == null)
                throw new StratumException(StratumError.MinusOne, "Missing request id");

            var context = connection.ContextAs<ETPWorkerContext>();
            var submitParams = request.ParamsAs<string[]>();
            if (submitParams == null || submitParams.Length != 3)
                throw new StratumException(StratumError.MinusOne, "Invalid parameters");

            logger.Info($"[{connection.ConnectionId}] Share submitted by miner {context.Worker} (Wallet: {context.Miner})");
            var share = await manager.SubmitShareAsync(connection, submitParams, context.Difficulty, CancellationToken.None);
            await connection.RespondAsync(true, request.Id);
            OnShare(share);
        }
    }
}
