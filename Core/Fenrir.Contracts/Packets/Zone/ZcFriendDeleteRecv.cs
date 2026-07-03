using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendDeleteRecv, ExpectedSize = 5)]
public readonly partial record struct ZcFriendDeleteRecv : IOutgoingPacket
{
    public required int Index { get; init; }
}
