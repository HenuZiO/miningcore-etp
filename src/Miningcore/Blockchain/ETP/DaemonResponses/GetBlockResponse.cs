using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class Block
    {
        public string Hash { get; set; }
        public long Height { get; set; }
        public string Version { get; set; }
        public string PreviousBlockHash { get; set; }
        public string MerkleTreeHash { get; set; }
        public long Time { get; set; }
        public string Bits { get; set; }
        public string Nonce { get; set; }
        public string MixHash { get; set; }
        public decimal Difficulty { get; set; }
        public string[] Transactions { get; set; }
        public string TransactionCount { get; set; }
        public string Size { get; set; }
    }

    public class GetBlockResponse
    {
        [JsonProperty("result")]
        public Block Block { get; set; }
    }
}
