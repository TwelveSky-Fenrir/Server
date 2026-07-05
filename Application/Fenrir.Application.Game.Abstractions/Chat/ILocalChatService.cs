using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Chat;

/// <summary>
///     Business logic for CZ_GENERAL_CHAT_SEND (opcode 38): the mute gate plus posting the AOI-scoped broadcast
///     onto the sender's own zone tick.
/// </summary>
public interface ILocalChatService
{
    /// <summary>
    ///     Posts <paramref name="content" />/<paramref name="link" /> as a <see cref="ChatBroadcastKind.Local" />
    ///     command onto <paramref name="zone" />'s tick. A muted sender is silently ignored -- returns false.
    /// </summary>
    public bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link);
}
