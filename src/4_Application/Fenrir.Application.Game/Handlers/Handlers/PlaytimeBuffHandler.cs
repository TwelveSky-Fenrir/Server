using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class PlaytimeBuffHandler(IPlaytimeBuffService service, ILogger<PlaytimeBuffHandler> logger)
    : IInlinePacketHandler<PlaytimeBuffRequest>
{
    public void Handle(in PlaytimeBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: PlaytimeBuffRequest (op97) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        var result = service.Apply(zone, characterId, packet.Sort);

        logger.LogInformation("Character {CharacterId} playtime-buff sort {Sort} resolved to value {Value}",
            characterId, packet.Sort, result.Value);

        session.Send(new AvatarStatUpdateResponse { Sort = 55, Value = result.Value, Value2 = 0 });
    }
}
