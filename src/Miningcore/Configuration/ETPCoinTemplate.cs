using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Miningcore.Configuration
{
    public class ETPCoinTemplate : CoinTemplate
    {
        public ETPCoinTemplate()
        {
            Family = CoinFamily.ETP;
        }

        public override string GetAlgorithmName()
        {
            return "ethash";
        }
    }
}
