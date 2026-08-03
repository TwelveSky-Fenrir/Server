using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class WorldChatHandler(IWorldChatService worldChatService) : IInlinePacketHandler<WorldChatRequest>
{
    public void Handle(in WorldChatRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        var content = ChatRouter.SafeContent(packet.Content);

        if (ChatRouter.IsContentEmpty(content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        worldChatService.TrySendChat(sender, content);
    }
}
