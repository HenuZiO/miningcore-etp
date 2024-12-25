using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Stratum;

namespace Miningcore.Blockchain.ETP
{
    public abstract class ETPJobManagerBase : JobManagerBase<ETPJob>
    {
        protected ETPJobManagerBase(IComponentContext ctx, IMessageBus messageBus) : base(ctx, messageBus)
        {
        }

        public ETPJob GetCurrentJob()
        {
            lock(jobLock)
            {
                return currentJob;
            }
        }

        protected override abstract void ConfigureDaemons();
        protected override abstract Task<bool> AreDaemonsHealthyAsync(CancellationToken ct);
        protected override abstract Task<bool> AreDaemonsConnectedAsync(CancellationToken ct);
        protected override abstract Task EnsureDaemonsSynchedAsync(CancellationToken ct);
        protected override abstract Task PostStartInitAsync(CancellationToken ct);
        protected abstract Task<(bool IsNew, bool Force)> UpdateJob(CancellationToken ct, bool forceUpdate, string via = null, string json = null);
        public abstract Task<Share> SubmitShareAsync(StratumConnection worker, string[] request, double stratumDifficulty, CancellationToken ct);
        public abstract Task<bool> ValidateAddressAsync(string address, CancellationToken ct);
        public abstract double HashrateFromShares(double shares, double interval);
    }
}
