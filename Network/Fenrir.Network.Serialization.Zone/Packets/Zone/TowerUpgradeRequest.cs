using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TowerUpgrade,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TowerUpgradeRequest : IIncomingPacket<TowerUpgradeRequest>
{
    public required int Index { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
