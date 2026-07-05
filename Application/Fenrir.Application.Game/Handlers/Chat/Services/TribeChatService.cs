using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

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
    bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link);
}

public sealed class TribeChatService : ITribeChatService
{
    public bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link)
    {
        if (sender.IsMuted)
            return false;

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = sender.CharacterId,
            Kind = ChatBroadcastKind.Tribe,
            Content = content,
            Link = link
        });

        return true;
    }
}
