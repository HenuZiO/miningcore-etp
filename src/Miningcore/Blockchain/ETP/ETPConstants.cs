using System;

namespace Miningcore.Blockchain.ETP
{
    public class ETPConstants
    {
        public const string ETPStratumVersion = "EthereumStratum/1.0.0";  // Changed to Ethereum compatible
        public const decimal MinimumDifficulty = 0.001m;
        public const string DifficultyUnit = "MH";
        
        public const int MinConfirmations = 60;
        
        public const decimal StaticTransactionFeeReserve = 0.0001m; // ETP
        public const decimal TransactionFeeReserve = 0.0001m; // ETP
        
        public static class RpcMethods
        {
            public const string GetInfo = "getinfo";
            public const string GetWork = "eth_getwork";
            public const string SubmitWork = "eth_submitwork";
            public const string ValidateAddress = "validateaddress";
            public const string GetPeerInfo = "getpeerinfo";
        }

        public static class StratumMethods
        {
            // Standard Stratum Methods
            public const string Subscribe = "mining.subscribe";
            public const string Authorize = "mining.authorize";
            public const string Submit = "mining.submit";
            public const string GetTransactions = "mining.get_transactions";
            public const string ExtraNonceSubscribe = "mining.extranonce.subscribe";
            
            // Ethereum Stratum Methods
            public const string EthSubmitLogin = "eth_submitLogin";
            public const string EthGetWork = "eth_getWork";
            public const string EthSubmitWork = "eth_submitWork";
            public const string EthSubmitHashrate = "eth_submitHashrate";
            
            // Notifications
            public const string SetDifficulty = "mining.set_difficulty";
            public const string MiningNotify = "mining.notify";
        }

        // Stratum constants
        public const string StratumSubscribe = "mining.subscribe";
        public const string StratumAuthorize = "mining.authorize";
        public const string StratumSubmit = "mining.submit";
        public const string DifficultyNotification = "mining.set_difficulty";
        public const string JobNotification = "mining.notify";

        public const uint ShareMultiplierRpc = 256;
        public const string DaemonRpcLocation = "/rpc/v3";
        public const string EthereumStratumVersion = "EthereumStratum/1.0.0";

        // Константы для расчета хешрейта
        public const double ShareMultiplier = 8192.0; // Множитель для ETP
        public const int TargetBlockTime = 30;  // Блок каждые 30 секунд
        // Множитель для расчета хешрейта из сложности
        // 2^32 = 4294967296
        public const double HashRateMultiplier = 4294967296.0;
        public const int DifficultyAdjustmentInterval = 2016; // blocks

        public static readonly System.Numerics.BigInteger BigMaxValue = System.Numerics.BigInteger.Pow(2, 256);

        // Extra nonce size in bytes
        public const int ExtraNonceBytes = 4;
    }
}
