using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op28, CZ_LOW_ITEM_SEND -- downgrades a Rare/Elite item to the previous tier (delegated to
///     <see cref="IDowngradeItemRankService" />). Unlike <see cref="UpgradeItemRankHandler" />, a successful
///     downgrade only swaps the item id -- Enchant/Combine/Refine/Socket are all left exactly as they were.
/// </summary>
public sealed class DowngradeItemRankHandler(
    IDowngradeItemRankService downgradeItemRankService,
    ILogger<DowngradeItemRankHandler> logger)
    : IAsyncPacketHandler<DowngradeItemRankRequest>
{
    public async ValueTask HandleAsync(DowngradeItemRankRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: DowngradeItemRankRequest received ({Page1}:{Index1} + {Page2}:{Index2})",
                zoneSession.SessionId, characterId, packet.Page1, packet.Index1, packet.Page2, packet.Index2);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: DowngradeItemRankRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await downgradeItemRankService.DowngradeAsync(packet, zone, state, characterId,
                cancellationToken);

            switch (result.Outcome)
            {
                case DowngradeItemRankOutcome.Rejected:
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                case DowngradeItemRankOutcome.NoCandidate:
                    session.Send(new DowngradeItemRankResponse
                    {
                        Result = 2, Cost = result.Cost, Value = result.Value
                    });
                    return;
            }

            session.Send(new DowngradeItemRankResponse
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
