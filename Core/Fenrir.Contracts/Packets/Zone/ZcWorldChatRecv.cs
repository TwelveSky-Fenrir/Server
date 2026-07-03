using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_WORLD_CHAT_RECV (ZONE.h:1197-1202, same typedef as ZC 139, <c>ZCP_WORLD_CHAT_RECV</c>).
///     <c>ProcessForRelay case 115</c> (S04_MyWork04.cpp:287-299): sent to ALL ready players of every
///     zone, no filter. Builder <c>B_WORLD_CHAT_RECV</c> (S05_MyTransfer.cpp:1846). Distinct C# record
///     from <see cref="ZcTribeNotifyRecv" /> despite the identical layout.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.WorldChatRecv, ExpectedSize = 79)]
public readonly partial record struct ZcWorldChatRecv : IOutgoingPacket
{
    /// <summary>Same semantics as <see cref="ZcTribeNotifyRecv.TribeRole" />: sender's tribe number, not a role.</summary>
    public required int TribeRole { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
}
