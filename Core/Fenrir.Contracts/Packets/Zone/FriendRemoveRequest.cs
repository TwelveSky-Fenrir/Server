using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FriendRemove, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct FriendRemoveRequest : IIncomingPacket<FriendRemoveRequest>
{
    public required int Index { get; init; }
}
