using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class LocalChatHandler(ILocalChatService localChatService) : IAsyncPacketHandler<LocalChatRequest>
{
    public async ValueTask HandleAsync(LocalChatRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await localChatService.TryPostChatAsync(zone, zoneSession, state, packet.Content, packet.Link,
            cancellationToken);
    }
}
