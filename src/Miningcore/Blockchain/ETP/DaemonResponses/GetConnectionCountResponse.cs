using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetConnectionCountResponse
    {
        [JsonProperty("count")]
        public int Count { get; set; }
    }
}
