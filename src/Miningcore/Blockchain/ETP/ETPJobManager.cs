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
            var response = await rpcClient.ExecuteAsync<GetMiningInfoResponse>(logger, ETPCommands.GetMiningInfo, ct);
            return response?.Error == null;
        }

        protected override async Task<bool> AreDaemonsConnectedAsync(CancellationToken ct)
        {
            var response = await rpcClient.ExecuteAsync<string[]>(logger, ETPCommands.GetPeerInfo, ct);
            return response?.Response?.Any() == true;
        }

        protected override async Task EnsureDaemonsSynchedAsync(CancellationToken ct)
        {
            var response = await rpcClient.ExecuteAsync<GetMiningInfoResponse>(logger, ETPCommands.GetMiningInfo, ct);
            
            if (response?.Response == null)
                throw new Exception("Unable to get mining info");

            if (response.Response.BlockType?.ToLower() != "pow")
                throw new Exception("Current block is not PoW");
        }

        protected override async Task PostStartInitAsync(CancellationToken ct)
        {
            await Task.CompletedTask;
        }

        public async Task<bool> UpdateJob(CancellationToken ct)
        {
            try
            {
                var response = await rpcClient.ExecuteAsync<GetMiningInfoResponse>(logger, ETPCommands.GetMiningInfo, ct);
                if (response?.Response == null)
                {
                    logger.Warn("Unable to update job. Daemon returned empty response.");
                    return false;
                }

                var jobId = NextJobId();
                var workResponse = await rpcClient.ExecuteAsync<string>(logger, ETPCommands.GetWork, ct);
                var blockHex = workResponse?.Response ?? string.Empty;
                
                var job = new ETPJob(
                    jobId,
                    response.Response.Height.ToString(),  // используем высоту как prevHash
                    blockHex,                            // используем шаблон блока из getwork
                    response.Response.Difficulty,
                    extraNonceProvider.Next(),
                    "",
                    DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                    true,
                    response.Response.Height
                );

                lock (jobLock)
                {
                    if (currentJob == null || job.Height > currentJob.Height)
                    {
                        currentJob = job;
                        jobSubject.OnNext(job);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return false;
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
