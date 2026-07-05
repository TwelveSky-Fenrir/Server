using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Commerce;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_SET_DEPUTY_PSHOP_MONEY_SEND (opcode 110), extracted from <see cref="WithdrawProxyShopEarningsHandler" />.</summary>
public interface IWithdrawProxyShopEarningsService
{
    /// <summary>
    ///     Withdraws accumulated offline-shop earnings into the character's live money. Requires the shop
    ///     closed and the submitted amounts to still match current earnings (CAS guard in
    ///     <see cref="OfflineShopRepository.WithdrawMoneyAsync" />).
    /// </summary>
    ValueTask<WithdrawProxyShopEarningsResponse> WithdrawAsync(int characterId, int money, int bigMoney,
        CancellationToken cancellationToken);
}

public sealed class WithdrawProxyShopEarningsService(
    IOfflineShopRepository offlineShops,
    ILogger<WithdrawProxyShopEarningsService> logger) : IWithdrawProxyShopEarningsService
{
    public async ValueTask<WithdrawProxyShopEarningsResponse> WithdrawAsync(int characterId, int money,
        int bigMoney, CancellationToken cancellationToken)
    {
        try
        {
            await offlineShops.WithdrawMoneyAsync(characterId, money, bigMoney, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop withdraw WithdrawMoneyAsync failed", characterId);
            return new WithdrawProxyShopEarningsResponse { Result = 3, Money = 0, BigMoney = 0 };
        }

        // Money/BigMoney echo the withdrawn amounts, not a running total.
        return new WithdrawProxyShopEarningsResponse { Result = 0, Money = money, BigMoney = bigMoney };
    }
}
