using System.Numerics;
using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP
{
    public class ETPBlockTemplate
    {
        [JsonProperty("height")]
        public ulong Height { get; set; }

        [JsonProperty("bits")]
        public string Bits { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("version")]
        public uint Version { get; set; }

        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty("headerhash")]
        public string HeaderHash { get; set; }

        [JsonProperty("mixhash")]
        public string MixHash { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("timestamp")]
        public ulong Timestamp { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }
    }
}
