using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GUILD_CHAT_SEND — same typedef as CZ 38 (CLIENT.h:193). Handler l.10736: empty content ⇒
///     <c>Quit()</c>; no guild ⇒ silent return; filtered by <c>CheckChat(GUILD_CHAT)</c> and mute.
///     Relayed via center tSort=112 — unlike <see cref="CzPartyChatSend" />, <see cref="Link" /> IS
///     transported — producing ZC 85 for guild members across every zone.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildChatSend, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzGuildChatSend : IIncomingPacket<CzGuildChatSend>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
