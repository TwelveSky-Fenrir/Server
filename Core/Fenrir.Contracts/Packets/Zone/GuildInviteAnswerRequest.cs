using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GUILD_ANSWER_SEND (CLIENT.h:373-376, struct shared with DUEL/TRADE/FRIEND/TEACHER/PARTY_ANSWER) —
///     the invitation target's reply (requires <c>mGuildProcessState==2</c>, S04_MyWork02.cpp:9918).
///     0 = accepts, 1/2 = refuses, other = silent return.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildInviteAnswer, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildInviteAnswerRequest : IIncomingPacket<GuildInviteAnswerRequest>
{
    public required int Answer { get; init; }
}
