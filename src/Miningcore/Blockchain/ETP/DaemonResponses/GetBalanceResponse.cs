using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetBalanceResponse
    {
        [JsonProperty("balance")]
        public string Balance { get; set; }
    }
}
