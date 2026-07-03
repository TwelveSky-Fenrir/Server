using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_WORLD_CHAT_SEND (CLIENT.h:198, <c>S_WORLD_CHAT_SEND</c>) — world chat. Handler l.15467: empty
///     content ⇒ <c>Quit()</c>; level &lt; 10 ⇒ <c>Quit()</c> (anti-bot spam); mute check;
///     <c>CheckChat</c> commented out. Relayed via center tSort=115 (same relay struct as 114, with the
///     sender's tribe) as ZC 192 to ALL players of every zone, no tribe filter.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.WorldChatSend, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzWorldChatSend : IIncomingPacket<CzWorldChatSend>
{
    [FixedString(61)] public required string Content { get; init; }
}
