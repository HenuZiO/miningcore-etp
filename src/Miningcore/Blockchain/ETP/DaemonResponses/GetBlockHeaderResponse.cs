using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetBlockHeaderResponse
    {
        [JsonProperty("height")]
        public long Height { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("previous_block_hash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty("bits")]
        public string Bits { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("mixhash")]
        public string Mixhash { get; set; }

        [JsonProperty("number")]
        public long Number { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("merkle_tree_hash")]
        public string MerkleTreeHash { get; set; }

        [JsonProperty("transaction_count")]
        public string TransactionCount { get; set; }
    }
}
