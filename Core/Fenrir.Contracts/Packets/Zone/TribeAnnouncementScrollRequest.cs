using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_TRIBE_NOTIFY_SEND (CLIENT.h:198, <c>S_TRIBE_NOTIFY_SEND</c>) — consumable tribe-announcement
///     scroll. Handler l.14114: empty content ⇒ <c>Quit()</c>; <c>wAvatar.aTribeNotifyNum &lt; 1</c> (no
///     scroll left) ⇒ <c>Quit()</c>; decrements <c>aTribeNotifyNum</c> and pushes ZC 22
///     (<c>S069FACTION_NOTICE_SCROLL</c>, new value) to the sender. <c>CheckChat</c> is commented out.
///     Relayed via center tSort=114 as ZC 139 to tribe members across every zone (tribe filter active,
///     LNW33).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAnnouncementScroll,
    ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeAnnouncementScrollRequest : IIncomingPacket<TribeAnnouncementScrollRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
