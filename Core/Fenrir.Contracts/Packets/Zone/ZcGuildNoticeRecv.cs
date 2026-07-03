using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GUILD_NOTICE_RECV (ZONE.h:849-853, <c>ZCS_GUILD_NOTICE_RECV</c>) — no ITEM_LINK_INFO (notice,
///     not chat). Emitted by <c>ProcessForRelay case 111</c> (S04_MyWork04.cpp:232-242) to players whose
///     <c>aGuildName</c> matches, across every zone. The relay's <c>tGuildName</c> never reaches the
///     client wire — it is only used for filtering.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildNoticeRecv, ExpectedSize = 75)]
public readonly partial record struct ZcGuildNoticeRecv : IOutgoingPacket
{
    /// <summary>Guild master (sender) name.</summary>
    [FixedString(13)]
    public required string AvatarName { get; init; }

    [FixedString(61)] public required string Content { get; init; }
}
