namespace Miningcore.Blockchain.ETP
{
    public class ETPStratumMethods
    {
        /// <summary>
        /// Used to subscribe to work from a stratum server, required before all other communication.
        /// </summary>
        public const string Subscribe = "mining.subscribe";

        /// <summary>
        /// Used to authorize a worker, required before any shares can be submitted.
        /// </summary>
        public const string Authorize = "mining.authorize";

        /// <summary>
        /// Used to submit shares
        /// </summary>
        public const string Submit = "mining.submit";

        /// <summary>
        /// Used to signal new work to the miner.
        /// </summary>
        public const string SetTarget = "mining.set_target";
        public const string SetDifficulty = "mining.set_difficulty";
        public const string Notify = "mining.notify";

        // Ethereum specific
        public const string SubmitLogin = "eth_submitLogin";
        public const string GetWork = "eth_getWork";
        public const string SubmitWork = "eth_submitWork";
        public const string SubmitHashrate = "eth_submitHashrate";
    }
}
