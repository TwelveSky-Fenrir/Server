using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Value01 must be 1..3, Value02 must be 2/4/6; requires items 666 (herb) + 1073 (bar).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TowerUpgrade,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TowerUpgradeRequest : IIncomingPacket<TowerUpgradeRequest>
{
    public required int Index { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
