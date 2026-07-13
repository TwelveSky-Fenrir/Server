using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
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
        var zoneSession = (ZoneClientSession)session;
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
        {
            logger.LogWarning(
                "Auto-potion threshold rejected for character {CharacterId}: life {LifeThreshold}/mana {ManaThreshold} out of range -- aborting session",
                characterId, packet.Value01, packet.Value02);
            zoneSession.Abort(DisconnectReason.Faulted);
        }
    }
}
