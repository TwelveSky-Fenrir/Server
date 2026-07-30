using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.EnterWorld,
    Compressed = true, ExpectedSize = 11449)]
public readonly partial record struct EnterWorldResponse : IOutgoingPacket
{
    public required AvatarInfo AvatarInfo { get; init; }
    public required BuffInfo BuffInfo { get; init; }
}
