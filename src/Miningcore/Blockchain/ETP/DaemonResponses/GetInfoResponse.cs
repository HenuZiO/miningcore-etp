using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetInfoResponse
    {
        [JsonProperty("database_version")]
        public string DatabaseVersion { get; set; }

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; }

        [JsonProperty("hash_rate")]
        public double HashRate { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("is_mining")]
        public bool IsMining { get; set; }

        [JsonProperty("asset_count")]
        public int AssetCount { get; set; }

        [JsonProperty("peers")]
        public int Peers { get; set; }

        [JsonProperty("protocol_version")]
        public int ProtocolVersion { get; set; }

        [JsonProperty("testnet")]
        public bool Testnet { get; set; }

        [JsonProperty("wallet_account_count")]
        public int WalletAccountCount { get; set; }

        [JsonProperty("wallet_version")]
        public string WalletVersion { get; set; }
    }
}
