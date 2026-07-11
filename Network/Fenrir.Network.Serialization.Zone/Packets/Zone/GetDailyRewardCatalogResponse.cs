using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetDailyRewardCatalog,
    ExpectedSize = 33)]
public readonly partial record struct GetDailyRewardCatalogResponse : IOutgoingPacket
{
    [FixedArray(7)] public required int[] RewardItem { get; init; }
    public required int RewardDay { get; init; }
}
