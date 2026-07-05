using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

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
    bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link);
}

public sealed class LocalChatService : ILocalChatService
{
    public bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link)
    {
        if (sender.IsMuted)
            return false;

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = sender.CharacterId,
            Kind = ChatBroadcastKind.Local,
            Content = content,
            Link = link
        });

        return true;
    }
}
