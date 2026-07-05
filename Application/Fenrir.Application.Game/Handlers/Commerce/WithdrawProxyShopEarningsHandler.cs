using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Commerce;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_SET_DEPUTY_PSHOP_MONEY_SEND (opcode 110) -- withdraw accumulated offline-shop earnings into the
///     character's live money. Requires the shop closed and the submitted amounts to still match current
///     earnings (CAS guard in <see cref="OfflineShopRepository.WithdrawMoneyAsync" />).
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

            // Money/BigMoney echo the withdrawn amounts, not a running total.
            session.Send(new WithdrawProxyShopEarningsResponse
                { Result = 0, Money = packet.Money, BigMoney = packet.BigMoney });
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }
}
