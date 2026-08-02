using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class UpgradeItemRankHandler(
    IUpgradeItemRankService upgradeItemRankService,
    ILogger<UpgradeItemRankHandler> logger)
    : IAsyncPacketHandler<UpgradeItemRankRequest>
{
    public async ValueTask HandleAsync(UpgradeItemRankRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UpgradeItemRankRequest received ({Page1}:{Index1} + {Page2}:{Index2})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1, packet.Page2, packet.Index2);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: UpgradeItemRankRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await upgradeItemRankService.UpgradeAsync(packet, zone, state, characterId,
                cancellationToken);

            switch (result.Outcome)
            {
                case UpgradeItemRankOutcome.Rejected:
                    session.Send(new UpgradeItemRankResponse { Result = 1, Cost = 0, Value = new int[6] });
                    return;
                case UpgradeItemRankOutcome.NoCandidate:
                    session.Send(new UpgradeItemRankResponse
                    {
                        Result = 2, Cost = result.Cost, Value = result.Value
                    });
                    return;
            }

            session.Send(new UpgradeItemRankResponse
            {
                Result = result.Succeeded ? 0 : 1, Cost = result.Cost, Value = result.Value
            });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
