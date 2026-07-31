using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class BuyShopItemHandler(IBuyShopItemService service, ILogger<BuyShopItemHandler> logger)
    : IAsyncPacketHandler<BuyShopItemRequest>
{
    public async ValueTask HandleAsync(BuyShopItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
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

    private async ValueTask HandleProxyPurchaseAsync(BuyShopItemRequest packet, IZoneSession zoneSession,
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
