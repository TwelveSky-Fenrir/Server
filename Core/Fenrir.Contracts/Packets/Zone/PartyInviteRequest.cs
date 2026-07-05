using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PartyInvite, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct PartyInviteRequest : IIncomingPacket<PartyInviteRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
