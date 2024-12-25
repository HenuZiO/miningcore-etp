using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Linq;
using Autofac;
using NLog;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.JsonRpc;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Notifications.Messages;
using Miningcore.Rpc;
using Miningcore.Stratum;
using Miningcore.Time;
using Miningcore.Util;
using Newtonsoft.Json;
using Contract = Miningcore.Contracts.Contract;
using Miningcore.Blockchain.ETP.Configuration;
using Miningcore.Blockchain.ETP.DaemonResponses;

namespace Miningcore.Blockchain.ETP
{
    public class ETPJobManager : JobManagerBase<ETPJob>
    {
        private DaemonEndpointConfig[] daemonEndpoints;
        private readonly IExtraNonceProvider extraNonceProvider;
        private ETPPoolConfigExtra extraPoolConfig;
        private RpcClient rpcClient;
        private readonly Subject<ETPJob> jobSubject = new Subject<ETPJob>();
        public IObservable<ETPJob> Jobs => jobSubject.AsObservable();

        public ETPJobManager(
            IComponentContext ctx,
            IMessageBus messageBus,
            IExtraNonceProvider extraNonceProvider) :
            base(ctx, messageBus)
        {
            Contract.RequiresNonNull(ctx, nameof(ctx));
            Contract.RequiresNonNull(messageBus, nameof(messageBus));
            Contract.RequiresNonNull(extraNonceProvider, nameof(extraNonceProvider));

            this.extraNonceProvider = extraNonceProvider;
        }

        public override void Configure(PoolConfig pc, ClusterConfig cc)
        {
            Contract.RequiresNonNull(pc);
            Contract.RequiresNonNull(cc);

            logger = LogUtil.GetPoolScopedLogger(typeof(ETPJobManager), pc);
            base.poolConfig = pc;
            clusterConfig = cc;

            if (pc.Extra != null)
                extraPoolConfig = pc.Extra.SafeExtensionDataAs<ETPPoolConfigExtra>();

            if (base.poolConfig.Daemons == null || base.poolConfig.Daemons.Length == 0)
                throw new PoolStartupException("No daemons configured");

            ConfigureDaemons();
        }

        protected override void ConfigureDaemons()
        {
            var daemonEndpoints = base.poolConfig.Daemons
                .Where(x => string.IsNullOrEmpty(x.Category))
                .ToArray();

            if (daemonEndpoints.Length == 0)
                throw new PoolStartupException("No daemons configured");

            this.daemonEndpoints = daemonEndpoints;

            rpcClient = new RpcClient(daemonEndpoints.First(), new JsonSerializerSettings(), messageBus, base.poolConfig.Id);
        }

        protected override async Task<bool> AreDaemonsHealthyAsync(CancellationToken ct)
        {
            try
            {
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(logger, ETPCommands.GetInfo, ct, new object[] { });

                if (response.Error != null)
                {
                    logger.Error(() => $"Error(s) reading daemon info: {response.Error.Message}");
                    return false;
                }

                return true;
            }

            catch(Exception)
            {
                return false;
            }
        }

        protected override async Task<bool> AreDaemonsConnectedAsync(CancellationToken ct)
        {
            try
            {
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(logger, ETPCommands.GetInfo, ct, new object[] { });

                if (response.Error != null)
                {
                    logger.Error(() => $"Error(s) reading daemon info: {response.Error.Message}");
                    return false;
                }

                return response.Response.Peers > 0;
            }

            catch(Exception)
            {
                return false;
            }
        }

        protected override async Task EnsureDaemonsSynchedAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

            var syncPendingNotificationShown = false;

            do
            {
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(logger, ETPCommands.GetInfo, ct, new object[] { });

                if (response.Error != null)
                {
                    logger.Error(() => $"Error(s) checking daemon sync status: {response.Error.Message}");
                    continue;
                }

                var isSynched = response.Response.Peers > 0;

                if (isSynched)
                {
                    logger.Info(() => "All daemons synched with blockchain");
                    break;
                }

                if (!syncPendingNotificationShown)
                {
                    logger.Info(() => "Daemon is still syncing with network. Manager will be started once synced");
                    syncPendingNotificationShown = true;
                }

                await timer.WaitForNextTickAsync(ct);
            } while(true);
        }

        protected override async Task PostStartInitAsync(CancellationToken ct)
        {
            // Nothing special to do
            await Task.CompletedTask;
        }

        private async Task UpdateJob(CancellationToken ct)
        {
            try
            {
                var response = await rpcClient.ExecuteAsync<GetBlockTemplateResponse>(logger,
                    ETPCommands.GetBlockTemplate, ct, new object[] { });

                if (response.Error != null)
                {
                    logger.Error(() => $"Error(s) updating job: {response.Error.Message}");
                    return;
                }

                // Generate extra nonce values
                response.Response.ExtraNonce1 = extraNonceProvider.Next();
                response.Response.ExtraNonce2 = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

                var job = currentJob = new ETPJob(response.Response, response.Response.Height, response.Response.Difficulty);

                jobSubject.OnNext(job);
            }
            catch(Exception ex)
            {
                logger.Error(ex, () => "Error(s) updating job");
            }
        }

        public void PrepareWorker(StratumConnection connection)
        {
            var context = connection.ContextAs<ETPWorkerContext>();

            if (context == null)
            {
                context = new ETPWorkerContext();
                connection.SetContext(context);
            }

            context.ExtraNonce1 = extraNonceProvider.Next();
            
            lock(jobLock)
            {
                if(currentJob != null)
                    context.Difficulty = currentJob.Difficulty;
                else
                    context.Difficulty = extraPoolConfig?.Difficulty ?? 100000;
            }
        }

        public ETPJob GetJob()
        {
            lock(jobLock)
            {
                return currentJob;
            }
        }

        public Task<Share> SubmitShareAsync(StratumConnection worker,
            string[] request, double difficulty, CancellationToken ct)
        {
            Contract.RequiresNonNull(worker, nameof(worker));
            Contract.RequiresNonNull(request, nameof(request));

            var context = worker.ContextAs<ETPWorkerContext>();
            
            var share = new Share
            {
                PoolId = base.poolConfig.Id,
                Difficulty = difficulty,
                IpAddress = worker.RemoteEndpoint?.Address?.ToString(),
                Miner = context?.Miner,
                Worker = context?.Worker,
                UserAgent = context?.UserAgent,
                Source = request[1],
                Created = DateTime.UtcNow
            };

            return Task.FromResult(share);
        }
    }
}
