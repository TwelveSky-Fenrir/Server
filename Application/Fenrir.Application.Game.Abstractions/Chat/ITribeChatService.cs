using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_TRIBE_CHAT_SEND (opcode 81): the mute gate plus posting the whole-zone broadcast
///     onto the sender's own zone tick -- zone-local only, no inter-zone relay; alliance not modeled.
/// </summary>
public interface ITribeChatService
{
    /// <summary>
    ///     Posts <paramref name="content" />/<paramref name="link" /> as a <see cref="ChatBroadcastKind.Tribe" />
    ///     command onto <paramref name="zone" />'s tick. A muted sender is silently ignored -- returns false.
    /// </summary>
    public bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link);
}
