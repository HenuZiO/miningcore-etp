using System;

namespace Miningcore.Blockchain.ETP
{
    public class GetWorkResult
    {
        public GetWorkResult(string[] response)
        {
            if (response == null || response.Length < 3)
                throw new ArgumentException("Invalid getwork response");

            HeaderHash = response[0];
            SeedHash = response[1];
            Target = response[2];
            
            // Generate a unique job ID
            JobId = Guid.NewGuid().ToString("N");
            
            // Initialize other fields
            PreviousBlockHash = HeaderHash;
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
