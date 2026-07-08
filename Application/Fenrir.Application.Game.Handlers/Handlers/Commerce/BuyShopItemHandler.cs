using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_BUY_PSHOP_SEND (opcode 35) -- purchase from either a registered OFFLINE/proxy shop (always
///     attempted first, only reachable inside the proxy-shop hub zone) or, failing that, a LIVE
///     personal-shop stall. The LIVE path is a two-character economy action: both participants'
///     <see cref="PlayerRuntimeState.EconomyActionLock" /> are held, smaller CharacterId first to avoid
///     deadlock. The proxy path only ever locks the BUYER's own lock, since the seller need not be online at
///     all. Gated by <see cref="NpcShopPolicy.TownZoneNumbers" />, deliberately asymmetric with the other
///     PShop opcodes' zone-37-only gate.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:6925-7304 (goto dispatch structure :6925-6957; LIVE path
///     :6953-7124; proxy path :7126-7304, mirrored logic also in Server/ts25zone/S07_MyGame09.cpp:805-816).
///     This handler's own <c>session</c> is always the BUYER's connection, so it only ever sends the
///     buyer-facing messages; on the LIVE path the SELLER's own notifications (item-sold, self-view listing
///     refresh, and -- if the sale emptied the stall -- stall-closed plus the AOI avatar-action broadcast)
///     are delivered on the seller's own connection by the zone tick that drains the <c>PshopZoneCommand</c>
///     this handler's service posts (<c>Zone.ApplyPshopCommand</c>) -- this handler must never itself send a
///     seller-facing packet, since <c>session</c> here never belongs to the seller
///     (Server/ts25zone/S04_MyWork02.cpp:7067-7071,7096-7100,7102-7120). The proxy path has no live seller
///     session to notify at all.
/// </remarks>
public sealed class BuyShopItemHandler(IBuyShopItemService service, ILogger<BuyShopItemHandler> logger)
    : IAsyncPacketHandler<BuyShopItemRequest>
{
    public async ValueTask HandleAsync(BuyShopItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var buyerId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        logger.LogDebug(
            "BuyShopItem: session {SessionId} character {CharacterId} seller {SellerAvatarName} slot {Page1}/{Index1}",
            session.SessionId, buyerId, packet.AvatarName, packet.Page1, packet.Index1);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(buyerId, out var buyer) || buyer is null)
            return;

        var lookup = await service.FindSellerAsync(packet, zone, buyer, buyerId, cancellationToken);
        switch (lookup.Outcome)
        {
            case BuyShopItemSellerOutcome.Abort:
                logger.LogWarning(
                    "Buy shop item rejected: character {CharacterId} request failed structural validation -- session will be disconnected",
                    buyerId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case BuyShopItemSellerOutcome.Reply:
                logger.LogDebug(
                    "Buy shop item rejected: character {CharacterId} seller lookup returned result {Result}",
                    buyerId, lookup.Reply!.Value.Result);
                session.Send(lookup.Reply!.Value);
                return;
            case BuyShopItemSellerOutcome.ProxyProceed:
                await HandleProxyPurchaseAsync(packet, zoneSession, zone, buyer, lookup, accountId,
                    cancellationToken);
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
                    logger.LogWarning(
                        "Buy shop item rejected: buyer {BuyerId}/seller {SellerId} commit failed structural validation -- session will be disconnected",
                        buyer.CharacterId, seller.CharacterId);
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

    /// <summary>
    ///     Commits a purchase against a registered, currently-open OFFLINE/proxy shop. Only the BUYER's own
    ///     <see cref="PlayerRuntimeState.EconomyActionLock" /> is held -- the seller's shop lives purely in
    ///     SQL, so no dual-lock/deadlock-ordering dance is needed here (unlike the LIVE path above), the same
    ///     posture <c>UpdateProxyShopHandler</c> already uses for its own BuySort=2 purchase variant.
    /// </summary>
    private async ValueTask HandleProxyPurchaseAsync(BuyShopItemRequest packet, ZoneClientSession zoneSession,
        Zone zone, PlayerRuntimeState buyer, BuyShopItemSellerResult lookup, int accountId,
        CancellationToken cancellationToken)
    {
        await buyer.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var commit = await service.CommitProxyPurchaseAsync(packet, zone, buyer, lookup.ProxySellerId,
                accountId, lookup.Slot, cancellationToken);
            if (commit.Abort)
            {
                logger.LogWarning(
                    "Buy shop item rejected: buyer {BuyerId}/proxy seller {SellerId} commit failed structural validation -- session will be disconnected",
                    buyer.CharacterId, lookup.ProxySellerId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            zoneSession.Send(commit.Response!.Value);
        }
        finally
        {
            buyer.EconomyActionLock.Release();
        }
    }
}
