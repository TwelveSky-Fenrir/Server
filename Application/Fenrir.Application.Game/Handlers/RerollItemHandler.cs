using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op26, CZ_EXCHANGE_ITEM_SEND -- rerolls a Rare/Elite equip item into a random same-tier/category
///     replacement (delegated to <see cref="IRerollItemService" />). <c>Sort</c>/<c>Value1</c>/<c>Value2</c> are
///     dead wire fields the legacy handler itself never reads.
/// </summary>
public sealed class RerollItemHandler(IRerollItemService rerollItemService)
    : IAsyncPacketHandler<RerollItemRequest>
{
    public async ValueTask HandleAsync(RerollItemRequest packet, IPacketSession session,
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
            var result = await rerollItemService.RerollAsync(packet, zone, state, characterId, cancellationToken);

            switch (result.Outcome)
            {
                case RerollItemOutcome.Rejected:
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                case RerollItemOutcome.NoCandidate:
                    session.Send(new RerollItemResponse { Result = 1, Cost = result.Cost, Value = result.Value });
                    return;
            }

            session.Send(new RerollItemResponse { Result = 0, Cost = result.Cost, Value = result.Value });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
