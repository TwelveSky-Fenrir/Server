using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class UpgradeCapeHandler(IUpgradeCapeService upgradeCapeService, ILogger<UpgradeCapeHandler> logger)
    : IAsyncPacketHandler<UpgradeCapeRequest>
{
    public async ValueTask HandleAsync(UpgradeCapeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: UpgradeCapeRequest (op127) received for character {CharacterId}",
            session.SessionId, characterId);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await upgradeCapeService.UpgradeAsync(packet, zone, state, characterId, cancellationToken);

            if (result.Outcome != UpgradeCapeOutcome.Applied)
            {
                logger.LogWarning(
                    "Cape-upgrade rejected for character {CharacterId} -- aborting session", characterId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            logger.LogInformation("Character {CharacterId} cape-upgrade resolved: succeeded={Succeeded}",
                characterId, result.Succeeded);

            session.Send(new UpgradeCapeResponse
            {
                Result = result.Succeeded ? 0 : 1,
                Value = result.Value,
                Padding = 0
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
