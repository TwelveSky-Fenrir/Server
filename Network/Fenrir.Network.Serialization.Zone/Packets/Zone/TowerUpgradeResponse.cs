using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TowerUpgrade,
    ExpectedSize = 25)]
public readonly partial record struct TowerUpgradeResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    [FixedArray(2)] public required int[] Page { get; init; }
    [FixedArray(2)] public required int[] Index { get; init; }
    public required int Count { get; init; }
}
