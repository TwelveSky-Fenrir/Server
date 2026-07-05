using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_BUY_PSHOP_SEND (opcode 35) -- purchase from a LIVE personal-shop stall. Two-character economy
///     action: both participants' <see cref="PlayerRuntimeState.EconomyActionLock" /> are held, smaller
///     CharacterId first to avoid deadlock. Gated by <see cref="Fenrir.Application.Game.World.Npcs.NpcShopPolicy.TownZoneNumbers" />,
///     deliberately asymmetric with the other PShop opcodes' zone-37-only gate.
/// </summary>
public sealed class BuyShopItemHandler(IBuyShopItemService service) : IAsyncPacketHandler<BuyShopItemRequest>
{
    public async ValueTask HandleAsync(BuyShopItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var buyerId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(buyerId, out var buyer) || buyer is null)
            return;

        var lookup = service.FindSeller(packet, zone, buyer, buyerId);
        switch (lookup.Outcome)
        {
            case BuyShopItemSellerOutcome.Abort:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case BuyShopItemSellerOutcome.Reply:
                session.Send(lookup.Reply!.Value);
                return;
        }

        var seller = lookup.Seller!;
        var slot = lookup.Slot;

        var (first, second) = buyer.CharacterId < seller.CharacterId ? (buyer, seller) : (seller, buyer);

        await first.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await second.EconomyActionLock.WaitAsync(cancellationToken);
            try
            {
                var commit = await service.CommitAsync(packet, zone, buyer, seller, slot, cancellationToken);
                if (commit.Abort)
                {
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                }

                session.Send(commit.Response!.Value);
                if (commit.ShopClosed)
                    session.Send(new CloseShopStallResponse { Result = 1 });
            }
            finally
            {
                second.EconomyActionLock.Release();
            }
        }
        finally
        {
            first.EconomyActionLock.Release();
        }
    }
}
