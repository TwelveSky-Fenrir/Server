using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class TribeChatHandler(ITribeChatService tribeChatService, ILogger<TribeChatHandler>? logger = null)
    : IInlinePacketHandler<TribeChatRequest>
{
    public void Handle(in TribeChatRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_TRIBE_CHAT_SEND received (character {CharacterId}, content length {ContentLength})",
            session.SessionId, zoneSession.CharacterId, packet.Content.Length);

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            // Server/ts25zone/S04_MyWork02.cpp:11239-11244 -- empty content is treated as a tampered client.
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        tribeChatService.TryPostChat(zone, state, packet.Content, packet.Link);
    }
}
