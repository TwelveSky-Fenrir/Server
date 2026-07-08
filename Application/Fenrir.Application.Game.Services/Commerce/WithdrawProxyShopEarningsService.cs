using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Data.SqlClient;
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

    /// <summary>
    ///     usp_OfflineShop_WithdrawMoney.sql's distinct THROW for a zero pending balance on both components
    ///     -- maps to the contract's own separate result 4 ("nothing to withdraw"), not the generic result 3
    ///     used for every other withdrawal failure (stale-client mismatch, shop not closed, or shop
    ///     expired).
    /// </summary>
    private const int NothingToWithdrawErrorNumber = 50340;

    public async ValueTask<WithdrawProxyShopEarningsResponse> WithdrawAsync(int characterId, int accountId,
        int money, int bigMoney, CancellationToken cancellationToken)
    {
        try
        {
            await offlineShops.WithdrawMoneyAsync(characterId, money, bigMoney, GameDate.Today(),
                cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == NothingToWithdrawErrorNumber)
        {
            // SQL 50340: both pending amounts are zero -- a distinct failure per the contract, not a
            // silent no-op success and not the generic stale/not-closed/expired result 3 below.
            logger.LogInformation(
                "Character {CharacterId} offline-shop withdraw denied: nothing to withdraw", characterId);
            return new WithdrawProxyShopEarningsResponse { Result = 4, Money = 0, BigMoney = 0 };
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

        logger.LogInformation(
            "Proxy shop earnings withdrawn: character {CharacterId} withdrew {Money}/{BigMoney} money delta ({MoneyBefore}/{BigMoneyBefore} -> {MoneyAfter}/{BigMoneyAfter})",
            characterId, money, bigMoney, moneyBefore, bigMoneyBefore, moneyAfter, bigMoneyAfter);

        // Money/BigMoney echo the withdrawn amounts, not a running total.
        return new WithdrawProxyShopEarningsResponse { Result = 0, Money = money, BigMoney = bigMoney };
    }
}
