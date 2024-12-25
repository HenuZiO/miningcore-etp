using System;
using Miningcore.Blockchain;

namespace Miningcore.Mining
{
    public class ETPExtraNonceProvider : IExtraNonceProvider
    {
        private uint counter;

        public int ByteSize => 4;

        public void InitializeInstance(byte[] instanceId)
        {
            // Not needed for ETP
        }

        public string Next()
        {
            lock(this)
            {
                ++counter;
                return counter.ToString("x8"); // Return 8-character hex string
            }
        }
    }
}
