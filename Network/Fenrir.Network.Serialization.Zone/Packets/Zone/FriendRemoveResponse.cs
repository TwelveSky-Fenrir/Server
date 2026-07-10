using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendRemove, ExpectedSize = 5)]
public readonly partial record struct FriendRemoveResponse : IOutgoingPacket
{
    public required int Index { get; init; }
}
