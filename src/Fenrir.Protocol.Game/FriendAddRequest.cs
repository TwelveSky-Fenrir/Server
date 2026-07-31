using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FriendAdd, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct FriendAddRequest : IIncomingPacket<FriendAddRequest>
{
    public required int Index { get; init; }
}
