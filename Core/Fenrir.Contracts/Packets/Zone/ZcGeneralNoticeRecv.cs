using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GENERAL_NOTICE_RECV (ZONE.h:443-446, <c>ZCS_GENERAL_NOTICE_RECV</c>) — world announcement.
///     Emitters: <c>ProcessForRelay case 102</c> (S04_MyWork04.cpp:25-36, relays CZ 17 from a GM, built
///     once and sent to every ready non-transferring player) and <c>MyUtil::BroadcastNotice</c>
///     (S07_MyGame03.cpp:767, system announcements). The relay casts the blob to <c>NOTICE_TOOL_RECV</c>
///     (<c>tContent[61]; int tTribe</c>) but the builder ignores <c>tTribe</c> — dead field on the relay
///     side, absent from the client wire.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GeneralNoticeRecv, ExpectedSize = 62)]
public readonly partial record struct ZcGeneralNoticeRecv : IOutgoingPacket
{
    [FixedString(61)] public required string Content { get; init; }
}
