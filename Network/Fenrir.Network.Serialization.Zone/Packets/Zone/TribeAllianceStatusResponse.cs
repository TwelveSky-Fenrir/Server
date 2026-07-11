using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAllianceStatus, ExpectedSize = 9)]
public readonly partial record struct TribeAllianceStatusResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
