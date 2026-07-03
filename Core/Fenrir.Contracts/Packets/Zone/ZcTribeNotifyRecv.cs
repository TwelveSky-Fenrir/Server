using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRIBE_NOTIFY_RECV (ZONE.h:1197-1202, <c>ZCS_TRIBE_NOTIFY_RECV</c>) — same C++ typedef as ZC
///     192. <c>ProcessForRelay case 114</c> (S04_MyWork04.cpp:265-286) calls
///     <c>B_TRIBE_NOTIFY_RECV(r->tTribe, …)</c>: the <c>tTribeRole=1</c> written by the CZ 112 handler
///     into the relay is DISCARDED, and it is <c>tTribe</c> that lands in the wire field
///     <see cref="TribeRole" /> (presumably used client-side for the tribe color/label). Recipient
///     filter: same tribe only (LNW33 branch active).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeNotifyRecv, ExpectedSize = 79)]
public readonly partial record struct ZcTribeNotifyRecv : IOutgoingPacket
{
    /// <summary>TRAP: actually carries the sender's TRIBE NUMBER (1-4), not a role, despite the field name.</summary>
    public required int TribeRole { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
}
