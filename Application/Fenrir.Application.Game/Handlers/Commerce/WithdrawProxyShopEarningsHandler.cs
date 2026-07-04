using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_SET_DEPUTY_PSHOP_MONEY_SEND (opcode 110, contracts/04_commerce.md, verified <c>S07_MyGame09.cpp:886-957</c>)
///     -- withdraw accumulated offline-shop earnings into the character's live money. Requires the shop
///     CLOSED (ShopState=0) and the submitted amounts to still match current earnings (anti-race
///     double-check, <see cref="OfflineShopRepository.WithdrawMoneyAsync" />'s own CAS guard). Shop
///     zero-out and money credit commit atomically.
/// </summary>
public sealed class WithdrawProxyShopEarningsHandler(
    IOfflineShopRepository offlineShops,
    ILogger<WithdrawProxyShopEarningsHandler> logger)
    : IAsyncPacketHandler<WithdrawProxyShopEarningsRequest>
{
    public async ValueTask HandleAsync(WithdrawProxyShopEarningsRequest packet, IPacketSession session,
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
            try
            {
                await offlineShops.WithdrawMoneyAsync(characterId, packet.Money, packet.BigMoney, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Character {CharacterId} offline-shop withdraw WithdrawMoneyAsync failed", characterId);
                session.Send(new WithdrawProxyShopEarningsResponse { Result = 3, Money = 0, BigMoney = 0 });
                return;
            }

            // Money/BigMoney echo the WITHDRAWN amounts (verified wire contract), not a running total --
            // not cached on PlayerRuntimeState (same posture as other money handlers, except Cash); no
            // mirror needed.
            session.Send(new WithdrawProxyShopEarningsResponse
                { Result = 0, Money = packet.Money, BigMoney = packet.BigMoney });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
