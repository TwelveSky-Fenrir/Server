using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_BUY_CASH_ITEM_SEND (opcode 42) -- purchase a cash-shop item. Price and the base granted
///     item/quantity are resolved entirely from
///     <see cref="Fenrir.Application.Game.Domain.Commerce.CommerceCatalogCache.CashCatalog" />'s
///     <c>CostInfoIndex</c> lookup -- most of the client's submitted <c>Value[6]</c> is never trusted, only
///     echoed back. The one exception is <c>Value[4]</c>, a legacy "custom bulk item mall" 1-99 multiplier
///     clamped and applied to the catalog quantity in
///     <see cref="Fenrir.Application.Game.Services.Commerce.BuyCashItemService" />
///     (Réf. C++ : <c>Server/ts25zone/S04_MyWork02.cpp:8208-8223</c>) -- the debit itself still only ever
///     charges for one catalog-defined unit, regardless of that multiplier.
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
