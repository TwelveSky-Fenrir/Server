using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendRemove, ExpectedSize = 5)]
public readonly partial record struct FriendRemoveResponse : IOutgoingPacket
{
    public required int Index { get; init; }
}
