using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetBlockTemplateResponse
    {
        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; }

        [JsonProperty("bits")]
        public string Bits { get; set; }

        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty("coinbasevalue")]
        public long CoinbaseValue { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("curtime")]
        public uint CurTime { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        // Additional fields needed for mining
        public string HeaderHash => Data;  // Current block header hash
        public string SeedHash => PreviousBlockHash;  // Seed hash for DAG
        public string JobId => Height.ToString();
        public string PrevHash => PreviousBlockHash;
        public string ExtraNonce1 { get; set; }
        public string ExtraNonce2 { get; set; }
        public string NTime => CurTime.ToString("x8");  // Time in hex format
    }
}
