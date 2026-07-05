using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FriendAdd, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct FriendAddRequest : IIncomingPacket<FriendAddRequest>
{
    public required int Index { get; init; }
}
