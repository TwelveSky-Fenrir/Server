using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarStatUpdate, ExpectedSize = 13)]
public readonly partial record struct AvatarStatUpdateResponse : IOutgoingPacket
{
    public required int Sort { get; init; }

    public required int Value { get; init; }

    public required int Value2 { get; init; }
}
