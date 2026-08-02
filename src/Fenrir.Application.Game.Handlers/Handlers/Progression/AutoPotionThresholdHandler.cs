using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Progression;

public sealed class AutoPotionThresholdHandler(
    IAutoPotionThresholdService autoPotionThresholdService,
    ILogger<AutoPotionThresholdHandler> logger)
    : IAsyncPacketHandler<AutoPotionThresholdRequest>
{
    public async ValueTask HandleAsync(AutoPotionThresholdRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: AutoPotionThresholdRequest (op86) received for character {CharacterId}, life {LifeThreshold} mana {ManaThreshold}",
            session.SessionId, characterId, packet.Value01, packet.Value02);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var result = await autoPotionThresholdService.ApplyAsync(characterId, state, packet.Value01, packet.Value02,
            cancellationToken);

        if (result.Aborted)
            logger.LogDebug(
                "Auto-potion threshold ignored for character {CharacterId}: life {LifeThreshold}/mana {ManaThreshold} out of valid range 0-5",
                characterId, packet.Value01, packet.Value02);
    }
}
