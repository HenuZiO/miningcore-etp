using System;
using Miningcore.Mining;

namespace Miningcore.Blockchain.ETP
{
    public class ETPWorkerContext : WorkerContextBase
    {
        public string ExtraNonce1;
        public double CurrentDifficulty;
        public ulong Hashrate { get; set; }
        public DateTime? LastShare { get; set; }
        public int ValidShares { get; set; }
        public int InvalidShares { get; set; }
        public string Miner { get; set; }  // Адрес майнера
        public string Worker { get; set; }  // Имя воркера
        public ETPJob CurrentJob { get; set; }
        public bool HasSetDifficulty { get; set; }

        public void UpdateShare(bool valid)
        {
            LastShare = DateTime.UtcNow;
            if (valid)
                ValidShares++;
            else
                InvalidShares++;
        }
    }

    public class ETPWorkerJob
    {
        public ETPJob Job { get; set; }
        public string Difficulty { get; set; }
    }
}
