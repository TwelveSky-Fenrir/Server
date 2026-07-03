using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GENERAL_CHAT_RECV (ZONE.h:594-601, <c>ZCS_GENERAL_CHAT_RECV</c>) — echo of the local chat (CZ
///     38), broadcast in AOI filtered by tribe/alliance. Also used unicast for the GM "where" replies.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.LocalChat, ExpectedSize = 99)]
public readonly partial record struct LocalChatResponse : IOutgoingPacket
{
    /// <summary>Sender's avatar name.</summary>
    [FixedString(13)]
    public required string AvatarName { get; init; }

    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
