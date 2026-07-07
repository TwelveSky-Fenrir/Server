using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class WithdrawProxyShopEarningsService(
    IOfflineShopRepository offlineShops,
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<WithdrawProxyShopEarningsService> logger) : IWithdrawProxyShopEarningsService
{
    /// <summary>
    ///     game.EventLog.EventCode for a proxy-shop earnings withdrawal row (legacy
    ///     <c>GL_1002_PXSHOP_MONEY</c>), scoped within <see cref="EventLogCategory.ProxyShop" /> -- see that
    ///     enum member's remarks for the full 1-4 numbering.
    /// </summary>
    private const short ProxyShopWithdrawEventCode = 4;

    public async ValueTask<WithdrawProxyShopEarningsResponse> WithdrawAsync(int characterId, int accountId,
        int money, int bigMoney, CancellationToken cancellationToken)
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

        // Logged only once WithdrawMoneyAsync above has durably committed, and only after -- so this row can
        // never assert a withdrawal that the DB write didn't actually persist, and always captures the
        // amount actually moved rather than usp_OfflineShop_WithdrawMoney's own subsequent zeroing of the
        // shop's stored earnings. MoneyBefore/BigMoneyBefore are derived from a single post-withdrawal read
        // (Before = After - the already-validated withdrawn amounts) rather than a separate pre-withdrawal
        // read: usp_OfflineShop_WithdrawMoney applies a plain `Money + @ExpectedMoney` /
        // `BigMoney + @ExpectedBigMoney` addition with no rollover branch (unlike
        // usp_OfflineShop_ExecutePurchase's BigMoney-overflow CASE WHEN), so this arithmetic is exact, not an
        // approximation, and it avoids a second full world-entry-bundle round trip just to log two integers.
        var bundle = await characters.GetWorldEntryBundleAsync(characterId, cancellationToken);
        var moneyAfter = bundle?.Character.Money ?? 0;
        var bigMoneyAfter = bundle?.Character.BigMoney ?? 0;
        var moneyBefore = moneyAfter - money;
        var bigMoneyBefore = bigMoneyAfter - bigMoney;

        await eventLog.LogAsync(ProxyShopWithdrawEventCode, EventLogCategory.ProxyShop, accountId, characterId,
            null, null, null, money, bigMoney, null, null, 1,
            $"MoneyBefore={moneyBefore};MoneyAfter={moneyAfter};BigMoneyBefore={bigMoneyBefore};" +
            $"BigMoneyAfter={bigMoneyAfter}",
            cancellationToken);

        // Money/BigMoney echo the withdrawn amounts, not a running total.
        return new WithdrawProxyShopEarningsResponse { Result = 0, Money = money, BigMoney = bigMoney };
    }
}
