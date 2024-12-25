using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class SubmitWorkResponse
    {
        [JsonProperty("result")]
        public bool Result { get; set; }
    }
}
