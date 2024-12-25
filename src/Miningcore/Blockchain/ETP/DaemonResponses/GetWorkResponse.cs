using Newtonsoft.Json;
using System;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetWorkResult
    {
        public GetWorkResult()
        {
        }

        [JsonConstructor]
        public GetWorkResult(
            string jobId = null,
            ulong height = 0,
            string bits = null,
            string target = null,
            uint version = 0,
            string previousblockhash = null,
            string mixhash = null,
            string nonce = null,
            ulong timestamp = 0)
        {
            JobId = jobId;
            Height = height;
            Bits = bits;
            Target = target;
            Version = version;
            PreviousBlockHash = previousblockhash;
            MixHash = mixhash;
            Nonce = nonce;
            Timestamp = timestamp;
        }

        public GetWorkResult(string[] data)
        {
            if (data != null && data.Length >= 3)
            {
                HeaderHash = data[0]?.Replace("0x", string.Empty);
                SeedHash = data[1]?.Replace("0x", string.Empty);
                Target = data[2]?.Replace("0x", string.Empty);
                
                // Используем текущее время, так как в массиве нет timestamp
                Timestamp = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }

        [JsonProperty("jobid")]
        public string JobId { get; set; }

        [JsonProperty("height")]
        public ulong Height { get; set; }

        [JsonProperty("bits")]
        public string Bits { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("version")]
        public uint Version { get; set; }

        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty("headerhash")]
        public string HeaderHash { get; set; }

        [JsonProperty("mixhash")]
        public string MixHash { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("timestamp")]
        public ulong Timestamp { get; set; }

        [JsonProperty("seedhash")]
        public string SeedHash { get; set; }
    }
}
