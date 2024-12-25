using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetMiningInfoResponse
    {
        [JsonProperty("mining_mst")]
        public string MiningMst { get; set; }

        [JsonProperty("block_type")]
        public string BlockType { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("is_mining")]
        public bool IsMining { get; set; }

        [JsonProperty("payment_address")]
        public string PaymentAddress { get; set; }

        [JsonProperty("rate")]
        public string Rate { get; set; }
    }
}
