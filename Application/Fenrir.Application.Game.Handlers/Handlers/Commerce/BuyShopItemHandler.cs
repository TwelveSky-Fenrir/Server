using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_BUY_PSHOP_SEND (opcode 35) -- purchase from a LIVE personal-shop stall. Two-character economy
///     action: both participants' <see cref="PlayerRuntimeState.EconomyActionLock" /> are held, smaller
///     CharacterId first to avoid deadlock. Gated by <see cref="NpcShopPolicy.TownZoneNumbers" />,
///     deliberately asymmetric with the other PShop opcodes' zone-37-only gate.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:6925-7124. This handler's own <c>session</c> is always the
///     BUYER's connection, so it only ever sends the buyer-facing messages (purchase-result, then listing
///     refresh); the SELLER's own notifications (item-sold, self-view listing refresh, and -- if the sale
///     emptied the stall -- stall-closed plus the AOI avatar-action broadcast) are delivered on the seller's
///     own connection by the zone tick that drains the <c>PshopZoneCommand</c> this handler's service posts
///     (<c>Zone.ApplyPshopCommand</c>) -- this handler must never itself send a seller-facing packet, since
///     <c>session</c> here never belongs to the seller (Server/ts25zone/S04_MyWork02.cpp:7067-7071,7096-7100,7102-7120).
/// </remarks>
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
                if (commit.ListingRefresh is { } listingRefresh)
                    session.Send(listingRefresh);
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
