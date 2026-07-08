using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class ShoutService : IShoutService
{
    public bool TryPostShout(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link)
    {
        if (!ChatRouter.IsShoutEnabledOnMap(zone.MapId))
            return false;

        if (sender.IsMuted)
            return false;

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = sender.CharacterId,
            Kind = ChatBroadcastKind.Shout,
            Content = content,
            Link = link
        });

        return true;
    }
}
