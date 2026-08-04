using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ContinueSkillStatHandler(
    IContinueSkillStatService service,
    ILogger<ContinueSkillStatHandler> logger)
    : IAsyncPacketHandler<ContinueSkillStatRequest>
{
    public async ValueTask HandleAsync(ContinueSkillStatRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: ContinueSkillStatRequest (op94) received for character {CharacterId}",
            session.SessionId, characterId);

        if (!await service.RegisterAutoBuffsAsync(zone, characterId, state, packet.Skill, cancellationToken))
        {
            logger.LogWarning(
                "Character {CharacterId} auto-buff registration was rejected by the zone actor", characterId);
            return;
        }

        logger.LogInformation("Character {CharacterId} registered auto-buff skill slots", characterId);
        session.Send(new AutoBuffRegisterResponse { Value = 0 });
    }
}
