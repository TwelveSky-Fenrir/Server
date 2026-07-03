using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_GENERAL_NOTICE_SEND (CLIENT.h:195-198, <c>S_GENERAL_NOTICE_SEND</c>) — GM global announcement.
///     Handler <c>W_GENERAL_NOTICE_SEND</c> (S04_MyWork02.cpp:1924) silently drops the packet when the
///     sender is not GM (<c>uUserSort &lt; 1</c>); otherwise relays to the center (tSort=102) for every
///     zone to rebuild ZC 20. Same typedef as CZ 76/80/112/152 (Content only, no ITEM_LINK_INFO).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GlobalAnnouncement, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GlobalAnnouncementRequest : IIncomingPacket<GlobalAnnouncementRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
