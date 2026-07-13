using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.EnterWorld,
    Compressed = true, ExpectedSize = 11449)]
public readonly partial record struct EnterWorldResponse : IOutgoingPacket
{
    public required AvatarInfo AvatarInfo { get; init; }
    public required BuffInfo BuffInfo { get; init; }
}
