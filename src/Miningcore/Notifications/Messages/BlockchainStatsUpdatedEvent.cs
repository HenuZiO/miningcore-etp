using System;

namespace Miningcore.Notifications.Messages;

public class BlockchainStatsUpdatedEvent
{
    public BlockchainStatsUpdatedEvent(string poolId)
    {
        PoolId = poolId;
    }

    public string PoolId { get; }
    public double NetworkHashrate { get; set; }
    public double NetworkDifficulty { get; set; }
    public ulong BlockHeight { get; set; }
    public int ConnectedPeers { get; set; }
}
