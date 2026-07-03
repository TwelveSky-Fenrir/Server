using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GUILD_ANSWER_RECV (ZONE.h:837-840, builder :1038) — dual use: (a) to the inviter, echoes the
///     target's <see cref="Answer" /> (0=accepted, 1/2=refused); (b) to the emitter of CZ_GUILD_ASK_SEND
///     as a pre-check failure (3=busy, 4=target not found, 5=target busy/transferring).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildInviteAnswer, ExpectedSize = 5)]
public readonly partial record struct GuildInviteAnswerResponse : IOutgoingPacket
{
    public required int Answer { get; init; }
}
