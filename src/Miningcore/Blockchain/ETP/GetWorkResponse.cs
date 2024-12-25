using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP
{
    public class GetWorkResponse
    {
        [JsonProperty("jobId")]
        public string JobId { get; set; }

        [JsonProperty("height")]
        public uint Height { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("header")]
        public string Header { get; set; }

        [JsonProperty("seed")]
        public string Seed { get; set; }
    }
}
