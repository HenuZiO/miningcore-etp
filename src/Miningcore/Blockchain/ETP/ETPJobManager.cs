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

        private new readonly IMessageBus messageBus;
        private readonly JsonSerializerSettings serializerSettings;
        private ILogger baseLogger;

        private readonly Subject<Share> shareSubject = new();
        private readonly Task persistenceTask;

        public ETPJobManager(
            IComponentContext ctx,
            IMessageBus messageBus,
            JsonSerializerSettings serializerSettings) : base(ctx, messageBus)
        {
            Contract.RequiresNonNull(messageBus);
            Contract.RequiresNonNull(serializerSettings);
            Contract.RequiresNonNull(ctx);

            this.messageBus = messageBus;
            this.serializerSettings = serializerSettings;
            
            // Initialize with a temporary logger
            this.baseLogger = LogManager.GetLogger(nameof(ETPJobManager));
            
            // Initialize persistence task
            persistenceTask = Task.CompletedTask;
        }

        public override void Configure(PoolConfig pc, ClusterConfig cc)
        {
            Contract.RequiresNonNull(pc);
            Contract.RequiresNonNull(cc);

            poolConfig = pc;
            clusterConfig = cc;
            extraPoolConfig = pc.Extra.SafeExtensionDataAs<ETPPoolConfigExtra>();

            // Now that we have poolConfig, set up the proper logger
            baseLogger = LogUtil.GetPoolScopedLogger(typeof(ETPJobManager), pc);

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
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(baseLogger,
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
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(baseLogger,
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
                var response = await rpcClient.ExecuteAsync<GetInfoResponse>(baseLogger,
                    ETPConstants.RpcMethods.GetInfo, ct, new object[] { });

                var isSynched = response.Error == null && response.Response?.Peers > 0;

                if(isSynched)
                {
                    baseLogger.Info(() => "All daemons synched with blockchain");
                    break;
                }

                if(!syncPendingNotificationShown)
                {
                    baseLogger.Info(() => "Daemon is still syncing with network. Manager will be started once synced");
                    syncPendingNotificationShown = true;
                }

                await timer.WaitForNextTickAsync(ct);
            } while(true);
        }

        protected override async Task PostStartInitAsync(CancellationToken ct)
        {
            // Get initial job
            await UpdateJobAsync(ct);

            // Start job polling in background
            Observable.Interval(TimeSpan.FromMilliseconds(poolConfig.JobRebroadcastTimeout))
                .Select(_ => Observable.FromAsync(() => UpdateJobAsync(ct)))
                .Concat()
                .Subscribe();

            await Task.CompletedTask;
        }

        private async Task UpdateJobAsync(CancellationToken ct)
        {
            try
            {
                var response = await rpcClient.ExecuteAsync<string[]>(baseLogger,
                    ETPConstants.RpcMethods.GetWork, ct, new object[] { });

                if (response?.Error != null)
                {
                    baseLogger.Error(() => $"Error during getwork: {response.Error.Message}");
                    return;
                }

                if (response?.Response == null || response.Response.Length == 0)
                {
                    baseLogger.Error(() => $"Got empty getwork response");
                    return;
                }

                // Create GetWorkResult from response
                var workResult = new GetWorkResult(response.Response);

                // Get current block number for height
                var blockResponse = await rpcClient.ExecuteAsync<GetInfoResponse>(baseLogger,
                    ETPConstants.RpcMethods.GetInfo, ct, new object[] { });

                if (blockResponse?.Error != null)
                {
                    baseLogger.Error(() => $"Error getting block number: {blockResponse.Error.Message}");
                    return;
                }

                workResult.Height = (ulong)blockResponse.Response.Height;

                // Create job
                var job = new ETPJob(workResult, workResult.Height, workResult.Target);

                lock(jobLock)
                {
                    currentJob = job;
                }

                messageBus.NotifyChainHeight(poolConfig.Id, (ulong)blockResponse.Response.Height, poolConfig.Template);

                // Broadcast to connected clients
                jobSubject.OnNext(job);

                baseLogger.Info(() => $"New job at height {job.Height}");
            }
            catch(Exception ex)
            {
                baseLogger.Error(ex, () => "Error during job update");
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
            baseLogger.Info($"[{connection.ConnectionId}] Setting difficulty to {difficulty} for worker {context.Worker ?? "Unknown"}");

            // Send current job
            var job = GetJob();
            if (job != null)
            {
                baseLogger.Info($"[{connection.ConnectionId}] Sending initial job to worker {context.Worker ?? "Unknown"}");
                connection.NotifyAsync(ETPConstants.StratumMethods.SetDifficulty, new object[] { context.CurrentDifficulty });
                connection.NotifyAsync(ETPConstants.StratumMethods.MiningNotify, job.GetJobParamsForStratum());
            }
            else
            {
                baseLogger.Warn($"[{connection.ConnectionId}] No job available for worker {context.Worker ?? "Unknown"}");
            }
        }

        public ETPJob GetJob()
        {
            lock(jobLock)
            {
                return currentJob;
            }
        }

        public async Task<Share> SubmitShareAsync(StratumConnection worker,
            string[] request,
            double stratumDifficulty,
            CancellationToken ct)
        {
            Contract.RequiresNonNull(worker);
            Contract.RequiresNonNull(request);

            var context = worker.ContextAs<ETPWorkerContext>();
            var nonce = request[0];
            var headerHash = request[1];
            var mixHash = request[2];

            ETPJob job;

            lock (jobLock)
            {
                job = currentJob;
            }

            if (job == null)
                throw new StratumException(StratumError.MinusOne, "job not found");

            // Create share
            var share = new Share
            {
                PoolId = poolConfig.Id,
                BlockHeight = (long)job.Height,
                Difficulty = stratumDifficulty,
                IpAddress = worker.RemoteEndpoint?.Address?.ToString(),
                Miner = context?.Miner,
                Worker = context?.Worker,
                UserAgent = context?.UserAgent,
                Source = headerHash,
                Created = DateTime.UtcNow
            };

            // Record share for API
            shareSubject.OnNext(share);

            // Record share for payout processor
            await persistenceTask;

            // Send block to daemon if the share meets difficulty
            if (share.Difficulty >= job.Difficulty)
            {
                share.IsBlockCandidate = true;

                baseLogger.Info(() => $"Submitting block {share.BlockHeight}");

                var acceptResponse = await rpcClient.ExecuteAsync<JsonRpcResponse<bool>>(baseLogger,
                    ETPConstants.RpcMethods.SubmitWork, ct, new object[]
                    {
                        nonce,
                        headerHash,
                        mixHash
                    });

                var isAccepted = (bool?)acceptResponse.Response?.Result ?? false;
                if (acceptResponse.Error != null || !isAccepted)
                {
                    baseLogger.Warn(() => $"Block {share.BlockHeight} submission failed with: {acceptResponse.Error?.Message ?? acceptResponse.Response?.Result.ToString()}");
                    messageBus.SendMessage(new AdminNotification("Block submission failed", $"Pool {poolConfig.Id} {(!string.IsNullOrEmpty(share.Source) ? $"[{share.Source.ToUpper()}] " : string.Empty)}failed to submit block {share.BlockHeight}: {acceptResponse.Error?.Message ?? acceptResponse.Response?.Result.ToString()}"));
                }
                else
                {
                    baseLogger.Info(() => $"Block {share.BlockHeight} accepted by network");
                    messageBus.SendMessage(new AdminNotification("Block accepted", $"Pool {poolConfig.Id} {(!string.IsNullOrEmpty(share.Source) ? $"[{share.Source.ToUpper()}] " : string.Empty)}submitted block {share.BlockHeight} [{share.BlockHash}]"));
                }
            }

            return share;
        }

        public double ShareMultiplier => ETPConstants.ShareMultiplier;
    }
}
