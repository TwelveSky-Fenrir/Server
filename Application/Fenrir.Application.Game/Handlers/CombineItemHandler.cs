using Fenrir.Application.Game.Handlers.ItemModification.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op25, CZ_ADD_ITEM_SEND -- the "combine" (IUValue) mechanic via <see cref="Combat.SystemRandomSource" /> and
///     <see cref="Forge.CombineResolver" /> (delegated to <see cref="ICombineItemService" />). A normal
///     (non-scroll) material survives a failed attempt; only scrolls (2001/2002/2003) are always consumed.
/// </summary>
public sealed class CombineItemHandler(ICombineItemService combineItemService)
    : IAsyncPacketHandler<CombineItemRequest>
{
    public async ValueTask HandleAsync(CombineItemRequest packet, IPacketSession session,
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
            var result = await combineItemService.CombineAsync(packet, zone, state, characterId, cancellationToken);

            if (result.Outcome != CombineItemOutcome.Applied)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(new CombineItemResponse { Result = result.ResultCode, Cost = result.Cost });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
