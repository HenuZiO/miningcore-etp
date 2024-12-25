using System;
using Miningcore.Blockchain.ETP;

namespace Miningcore.Notifications.Messages
{
    public class WorkerNotification
    {
        public WorkerNotification(string poolId, ETPJob job)
        {
            PoolId = poolId;
            Job = job;
            Created = DateTime.UtcNow;
        }

        public string PoolId { get; }
        public ETPJob Job { get; }
        public DateTime Created { get; }
    }
}
