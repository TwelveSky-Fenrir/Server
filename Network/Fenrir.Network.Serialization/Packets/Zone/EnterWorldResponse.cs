using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.EnterWorld,
    Compressed = true, ExpectedSize = 11449)]
public readonly record struct EnterWorldResponse : IOutgoingPacket
{
    public required AvatarInfo AvatarInfo { get; init; }
    public required BuffInfo BuffInfo { get; init; }
}
