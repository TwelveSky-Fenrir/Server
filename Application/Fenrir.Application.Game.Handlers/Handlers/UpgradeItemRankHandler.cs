using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op27, CZ_HIGH_ITEM_SEND -- upgrades a +4/combine&gt;=1 Rare/Elite item to the next tier (delegated to
///     <see cref="IUpgradeItemRankService" />). Cost is charged and the material consumed on both success and
///     roll-failure; only a missing replacement candidate (Result=2) skips both.
/// </summary>
public sealed class UpgradeItemRankHandler(IUpgradeItemRankService upgradeItemRankService)
    : IAsyncPacketHandler<UpgradeItemRankRequest>
{
    public async ValueTask HandleAsync(UpgradeItemRankRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await upgradeItemRankService.UpgradeAsync(packet, zone, state, characterId,
                cancellationToken);

            switch (result.Outcome)
            {
                case UpgradeItemRankOutcome.Rejected:
                    zoneSession.Abort(DisconnectReason.Faulted);
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
