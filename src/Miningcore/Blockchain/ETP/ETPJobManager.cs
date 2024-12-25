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
using Miningcore.Notifications;
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
        private ETPPoolConfigExtra extraPoolConfig;
        private RpcClient rpcClient;
        private readonly Subject<ETPJob> jobSubject = new();
        public IObservable<ETPJob> Jobs => jobSubject.AsObservable();

        private readonly IMessageBus messageBus;
        private readonly JsonSerializerSettings serializerSettings;

        public ETPJobManager(
            IComponentContext ctx,
            IMessageBus messageBus,
            IExtraNonceProvider extraNonceProvider) :
            base(ctx, messageBus)
        {
            Contract.RequiresNonNull(ctx, nameof(ctx));
            Contract.RequiresNonNull(messageBus, nameof(messageBus));
            Contract.RequiresNonNull(extraNonceProvider, nameof(extraNonceProvider));

            this.messageBus = messageBus;
            this.serializerSettings = ctx.Resolve<JsonSerializerSettings>();
        }

        public override void Configure(PoolConfig pc, ClusterConfig cc)
        {
            Contract.RequiresNonNull(pc);
            Contract.RequiresNonNull(cc);

            logger = LogUtil.GetPoolScopedLogger(typeof(ETPJobManager), pc);
            poolConfig = pc;
            clusterConfig = cc;
            extraPoolConfig = pc.Extra.SafeExtensionDataAs<ETPPoolConfigExtra>();

            // Extract daemon endpoints
            daemonEndpoints = pc.Daemons
                .Where(x => string.IsNullOrEmpty(x.Category))
                .ToArray();

            ConfigureDaemons();
        }

        protected override void ConfigureDaemons()
        {
            if(daemonEndpoints.Length == 0)
                throw new PoolStartupException("No daemons configured");

            rpcClient = new RpcClient(daemonEndpoints[0], serializerSettings, messageBus, poolConfig.Id);
        }

        protected override async Task<bool> AreDaemonsHealthyAsync(CancellationToken ct)
        {
            try
            {
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(logger,
                    ETPConstants.RpcMethods.GetInfo, ct, new object[] { });

                return response.Error == null;
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
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(logger,
                    ETPConstants.RpcMethods.GetInfo, ct, new object[] { });

                return response.Error == null && response.Response?.Peers > 0;
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
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(logger,
                    ETPConstants.RpcMethods.GetInfo, ct, new object[] { });

                var isSynched = response.Error == null && response.Response?.Peers > 0;

                if(isSynched)
                {
                    logger.Info(() => "All daemons synched with blockchain");
                    break;
                }

                if(!syncPendingNotificationShown)
                {
                    logger.Info(() => "Daemon is still syncing with network. Manager will be started once synced");
                    syncPendingNotificationShown = true;
                }

                await timer.WaitForNextTickAsync(ct);
            } while(true);
        }

        protected override async Task PostStartInitAsync(CancellationToken ct)
        {
            // Periodically update work
            using var timer = new System.Timers.Timer(5000);
            timer.Elapsed += async (sender, e) =>
            {
                try
                {
                    await UpdateJobAsync(ct);
                }
                catch(Exception ex)
                {
                    logger.Error(ex);
                }
            };

            timer.Start();

            await Task.CompletedTask;
        }

        private async Task UpdateJobAsync(CancellationToken ct)
        {
            try
            {
                var response = await rpcClient.ExecuteAsync<string[]>(logger,
                    ETPConstants.RpcMethods.GetWork, ct);

                if (response?.Error != null)
                {
                    logger.Error(() => $"Error during getwork: {response.Error.Message}");
                    return;
                }

                // Create GetWorkResult from response
                var workResult = new GetWorkResult(response.Response);

                // Get current block number for height
                var blockResponse = await rpcClient.ExecuteAsync<GetInfoResponse>(logger,
                    ETPConstants.RpcMethods.GetInfo, ct, new object[] { });

                if (blockResponse?.Error != null)
                {
                    logger.Error(() => $"Error getting block number: {blockResponse.Error.Message}");
                    return;
                }

                workResult.Height = (ulong)blockResponse.Response.Height;

                // Create job
                var job = new ETPJob(workResult, workResult.Height, workResult.Target);

                lock(jobLock)
                {
                    currentJob = job;
                }

                jobSubject.OnNext(job);

                logger.Info(() => $"New job at height {job.Height}");
            }
            catch(Exception ex)
            {
                logger.Error(ex, () => "Error during job update");
            }
        }

        protected void BroadcastJob(ETPJob job)
        {
            jobSubject.OnNext(job);
        }

        public void PrepareWorker(StratumConnection connection)
        {
            var context = connection.ContextAs<ETPWorkerContext>();
            var difficulty = context?.CurrentDifficulty ?? (double)ETPConstants.MinimumDifficulty;

            // setup worker context
            context = connection.ContextAs<ETPWorkerContext>();
            if (context == null)
            {
                context = new ETPWorkerContext();
                connection.SetContext(context);
            }

            context.CurrentDifficulty = difficulty;
            logger.Info($"[{connection.ConnectionId}] Setting difficulty to {difficulty} for worker {context.Worker ?? "Unknown"}");

            // Send current job
            var job = GetJob();
            if (job != null)
            {
                logger.Info($"[{connection.ConnectionId}] Sending initial job to worker {context.Worker ?? "Unknown"}");
                connection.NotifyAsync(ETPConstants.StratumMethods.SetDifficulty, new object[] { context.CurrentDifficulty });
                connection.NotifyAsync(ETPConstants.StratumMethods.MiningNotify, job.GetJobParamsForStratum());
            }
            else
            {
                logger.Warn($"[{connection.ConnectionId}] No job available for worker {context.Worker ?? "Unknown"}");
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
