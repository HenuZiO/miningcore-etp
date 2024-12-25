using System;
using System.Linq;
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
                        manager.PrepareWorker(connection);
                        await connection.RespondAsync(true, request.Id);
                        break;

                    case ETPConstants.StratumMethods.Authorize:
                        var authParams = request.ParamsAs<string[]>();
                        var workerValue = authParams?.Length > 0 ? authParams[0] : null;

                        if (!string.IsNullOrEmpty(workerValue))
                        {
                            // Split worker from wallet if format is wallet.worker
                            var split = workerValue.Split('.');
                            context.Worker = split.Length > 1 ? split[1] : split[0];
                            context.Miner = split[0];
                        }

                        // Initialize worker
                        manager.PrepareWorker(connection);

                        // Send successful response with worker name
                        await connection.RespondAsync(true, request.Id);
                        break;

                    case ETPConstants.StratumMethods.EthSubmitLogin:
                        if (request.Id == null)
                            throw new StratumException(StratumError.MinusOne, "Missing request id");

                        var loginParams = request.ParamsAs<string[]>();
                        if (loginParams == null || loginParams.Length < 1)
                            throw new StratumException(StratumError.MinusOne, "Invalid parameters");

                        var loginWorker = loginParams[0];

                        if (!string.IsNullOrEmpty(loginWorker))
                        {
                            // Split worker from wallet if format is wallet.worker
                            var split = loginWorker.Split('.');
                            context.Worker = split.Length > 1 ? split[1] : split[0];
                            context.Miner = split[0];
                        }

                        // Initialize worker
                        manager.PrepareWorker(connection);

                        // Send successful response
                        await connection.RespondAsync(true, request.Id);
                        break;

                    case ETPConstants.StratumMethods.EthGetWork:
                        if (context == null)
                            throw new StratumException(StratumError.MinusOne, "Context not initialized");

                        var job = manager.GetJob();
                        if (job != null)
                        {
                            await connection.RespondAsync(job.GetJobParamsForStratum(), request.Id);
                            logger.Debug($"[{connection.ConnectionId}] Sent work to miner: {context.Worker ?? "Unknown"}");
                        }
                        else
                        {
                            await connection.RespondAsync(new object[] { }, request.Id);
                            logger.Debug($"[{connection.ConnectionId}] No work available for miner: {context.Worker ?? "Unknown"}");
                        }
                        break;

                    case ETPConstants.StratumMethods.EthSubmitWork:
                        if (context == null)
                            throw new StratumException(StratumError.MinusOne, "Context not initialized");

                        if (request.Id == null)
                            throw new StratumException(StratumError.MinusOne, "Missing request id");

                        var submitParams = request.ParamsAs<string[]>();
                        if (submitParams == null || submitParams.Length != 3)
                            throw new StratumException(StratumError.MinusOne, "Invalid parameters");

                        var share = await manager.SubmitShareAsync(connection, submitParams, context.Difficulty, ct);
                        await connection.RespondAsync(true, request.Id);
                        OnShare(share);
                        break;

                    default:
                        logger.Debug($"[{connection.ConnectionId}] Unsupported method: {request.Method}");
                        await connection.RespondErrorAsync(StratumError.Other, $"Unsupported method: {request.Method}", request.Id);
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

        protected override async Task SetupJobManager(CancellationToken ct)
        {
            manager = ctx.Resolve<ETPJobManager>();
            manager.Configure(poolConfig, clusterConfig);

            await manager.StartAsync(ct);

            if (poolConfig.EnableInternalStratum == true)
            {
                disposables.Add(manager.Jobs.Subscribe(job =>
                {
                    // Send job to connected workers
                    ForEachMinerAsync(async (client, _) =>
                    {
                        var context = client.ContextAs<ETPWorkerContext>();
                        await client.NotifyAsync(ETPConstants.StratumMethods.SetDifficulty, new object[] { context.Difficulty });
                        await client.NotifyAsync(ETPConstants.StratumMethods.MiningNotify, job.GetJobParamsForStratum());
                    });
                }));

                // Monitor stratum connection events
                disposables.Add(manager.Jobs.Subscribe(job => 
                {
                    logger.Info(() => $"New job received at height {job.Height}");
                }));
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
    }
}
