using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Services.Chat;

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
