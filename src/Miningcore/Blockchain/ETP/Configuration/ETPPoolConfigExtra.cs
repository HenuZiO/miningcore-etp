using Newtonsoft.Json;

namespace Miningcore.Blockchain.ETP.Configuration
{
    public class ETPPoolConfigExtra
    {
        /// <summary>
        /// Base directory for DAG files
        /// </summary>
        [JsonProperty("dagdir")]
        public string DagDir { get; set; }

        /// <summary>
        /// Enable hot-wallet mode
        /// </summary>
        [JsonProperty("enableDaemonHotWallet")]
        public bool EnableDaemonHotWallet { get; set; }

        /// <summary>
        /// Account name for hot-wallet
        /// </summary>
        [JsonProperty("accountname")]
        public string AccountName { get; set; }

        /// <summary>
        /// Account password for hot-wallet
        /// </summary>
        [JsonProperty("accountpassword")]
        public string AccountPassword { get; set; }

        /// <summary>
        /// Default difficulty for new miners
        /// </summary>
        [JsonProperty("difficulty")]
        public double Difficulty { get; set; } = 100000;
    }

    public class ETPDaemonEndpointConfigExtra
    {
        /// <summary>
        /// Optional port for HTTP transport
        /// </summary>
        public int? HttpPort { get; set; }

        /// <summary>
        /// Optional port for Stratum port
        /// </summary>
        public int? StratumPort { get; set; }
    }

    public class ETPPaymentProcessingConfigExtra
    {
        public decimal MinimumConfirmations { get; set; }
    }
}
