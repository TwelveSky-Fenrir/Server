using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.EnterWorld,
    Compressed = true, ExpectedSize = 11449)]
public readonly partial record struct EnterWorldResponse : IOutgoingPacket
{
    public required AvatarInfo AvatarInfo { get; init; }
    public required BuffInfo BuffInfo { get; init; }
}
