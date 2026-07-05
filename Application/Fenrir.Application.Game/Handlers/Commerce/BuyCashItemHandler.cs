using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_BUY_CASH_ITEM_SEND (opcode 42) -- purchase a cash-shop item. Price and granted item/quantity are
///     resolved entirely from <see cref="Fenrir.Application.Game.GameData.WorldDataCache.CashCatalog" />'s
///     <c>CostInfoIndex</c> lookup -- the client's submitted <c>Value[6]</c> is never trusted, only echoed back.
/// </summary>
public sealed class BuyCashItemHandler(IBuyCashItemService service) : IAsyncPacketHandler<BuyCashItemRequest>
{
    public async ValueTask HandleAsync(BuyCashItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await service.ResolveAndApplyAsync(packet, zone, state, characterId, accountId,
                cancellationToken);
            if (result is null)
            {
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            session.Send(result.Value);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
