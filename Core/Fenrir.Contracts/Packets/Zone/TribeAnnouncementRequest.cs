using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_TRIBE_NOTICE_SEND — same typedef as CZ 17 (CLIENT.h:198). Handler l.11488: empty content ⇒
///     <c>Quit()</c>; restricted to tribe master/vice-master (<c>ReturnTribeRole(...) == 0</c> ⇒ return;
///     1 = master, 2 = vice-master). Relayed via center tSort=113 (role + name + content + tribe number)
///     so every zone rebuilds ZC 89 for members of the same tribe.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAnnouncement, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeAnnouncementRequest : IIncomingPacket<TribeAnnouncementRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
