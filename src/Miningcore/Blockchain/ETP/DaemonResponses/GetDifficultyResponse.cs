using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.DaemonResponses
{
    public class GetDifficultyResponse
    {
        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }
    }
}
