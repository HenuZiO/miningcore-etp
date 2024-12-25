using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetBlockchainInfoResponse
    {
        [JsonProperty("blocks")]
        public uint Blocks { get; set; }

        [JsonProperty("headers")]
        public uint Headers { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }
    }
}
