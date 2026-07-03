using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GUILD_FIND_SEND (CLIENT.h:315-319, same struct as CZ_GUILD_ASK_SEND) — locates a guild member
///     (requires being in a guild, silent return otherwise, S04_MyWork02.cpp:10787). Relayed synchronously
///     to ts25playuser (<c>U_ZONE_FIND_AVATAR_FOR_PLAYUSER_SEND</c>), answered with ZC_GUILD_FIND_RECV.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FindGuildMember, ExpectedSize = 22,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct FindGuildMemberRequest : IIncomingPacket<FindGuildMemberRequest>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
