using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Requires the caller to be in a guild; silent no-op otherwise.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FindGuildMember, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct FindGuildMemberRequest : IIncomingPacket<FindGuildMemberRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
