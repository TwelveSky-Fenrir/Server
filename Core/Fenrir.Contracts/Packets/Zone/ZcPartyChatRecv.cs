using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_PARTY_CHAT_RECV (ZONE.h:791-798, USE_ITEM_LINK_V2 ON in EU33 ⇒ <c>tLink</c> present). Same layout family as ZC
///     41/43/85/90 (chat lot). <see cref="ItemLinkInfo" /> is zeroed when no item is linked.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyChatRecv, ExpectedSize = 99)]
public readonly partial record struct ZcPartyChatRecv : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
