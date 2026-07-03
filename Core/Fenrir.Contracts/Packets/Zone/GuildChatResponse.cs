using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GUILD_CHAT_RECV (ZONE.h:855-862, <c>ZCS_GUILD_CHAT_RECV</c>) — layout identical to ZC 41.
///     Emitted by <c>ProcessForRelay case 112</c> (S04_MyWork04.cpp:243-253), filtered by
///     <c>aGuildName</c>; <see cref="Link" /> is transported end-to-end.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildChat, ExpectedSize = 99)]
public readonly partial record struct GuildChatResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
