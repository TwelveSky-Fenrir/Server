using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GUILD_NOTICE_SEND — same typedef as CZ 17 (CLIENT.h:198, <c>S_GUILD_NOTICE_SEND</c>). Handler
///     l.10705: empty content ⇒ <c>Quit()</c>; restricted to the guild master (<c>aGuildRole != 0</c> ⇒
///     silent return; 0 = master). No CheckChat/mute check. Relayed via center tSort=111 so every zone
///     rebuilds ZC 84 for members matching exact <c>aGuildName</c>.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildNoticeSend, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzGuildNoticeSend : IIncomingPacket<CzGuildNoticeSend>
{
    [FixedString(61)] public required string Content { get; init; }
}
