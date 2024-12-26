using System;

namespace Miningcore.Blockchain.ETP
{
    public class GetWorkResult
    {
        public GetWorkResult()
        {
            // Initialize fields
            JobId = Guid.NewGuid().ToString("N");
            ExtraNonce1 = string.Empty;
            ExtraNonce2 = string.Empty;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public string HeaderHash { get; set; }
        public string SeedHash { get; set; }
        public string Target { get; set; }
        public ulong Height { get; set; }
        
        // Additional fields needed by ETPJob
        public string JobId { get; set; }
        public string PreviousBlockHash { get; set; }
        public string ExtraNonce1 { get; set; }
        public string ExtraNonce2 { get; set; }
        public long Timestamp { get; set; }
    }
}
