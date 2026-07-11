using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FriendLocate, ExpectedSize = 9)]
public readonly partial record struct FriendLocateResponse : IOutgoingPacket
{
    public required int Index { get; init; }
    public required int ZoneNumber { get; init; }
}
