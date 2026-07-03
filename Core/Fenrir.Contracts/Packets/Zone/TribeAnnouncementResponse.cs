using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRIBE_NOTICE_RECV (ZONE.h:881-886, <c>ZCS_TRIBE_NOTICE_RECV</c>). Emitted by
///     <c>ProcessForRelay case 113</c> (S04_MyWork04.cpp:254-264), strict <c>aTribe == tTribe</c> filter
///     (no alliance). Here <see cref="TribeRole" /> genuinely carries a ROLE (contrast with ZC 139/192,
///     same layout, different semantics).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAnnouncement, ExpectedSize = 79)]
public readonly partial record struct TribeAnnouncementResponse : IOutgoingPacket
{
    /// <summary>Sender's real role: 1 = tribe master, 2 = vice-master.</summary>
    public required int TribeRole { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
}
