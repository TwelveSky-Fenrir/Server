using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendRemove, ExpectedSize = 5)]
public readonly partial record struct FriendRemoveResponse : IOutgoingPacket
{
    public required int Index { get; init; }
}
