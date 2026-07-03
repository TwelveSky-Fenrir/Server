using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRIBE_CHAT_RECV (ZONE.h:888-895, <c>ZCS_TRIBE_CHAT_RECV</c>) — layout identical to ZC 41.
///     Local to the zone (handler for CZ 81, l.11544-11556): recipients are the same tribe or an allied
///     tribe. No inter-zone relay.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeChatRecv, ExpectedSize = 99)]
public readonly partial record struct ZcTribeChatRecv : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
