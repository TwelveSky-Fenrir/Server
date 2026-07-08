using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>RewardDay is the next claimable day (0-6); 7 means all 7 days are already claimed.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetDailyRewardCatalog,
    ExpectedSize = 33)]
public readonly record struct GetDailyRewardCatalogResponse : IOutgoingPacket
{
    [FixedArray(7)] public required int[] RewardItem { get; init; }
    public required int RewardDay { get; init; }
}
