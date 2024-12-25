using Miningcore.Configuration;
using Newtonsoft.Json.Linq;

namespace Miningcore.Blockchain.ETP
{
    public class ETPCoinTemplate : CoinTemplate
    {
        public double ShareMultiplier { get; set; }
        public JToken PayoutSchemeConfig { get; set; }

        public override string GetAlgorithmName()
        {
            return "ethash";
        }
    }
}
