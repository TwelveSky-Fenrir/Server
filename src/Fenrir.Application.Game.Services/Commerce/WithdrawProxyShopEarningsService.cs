using CaeriusNet.Exceptions;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Protocol.Game;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class WithdrawProxyShopEarningsService(
    IOfflineShopRepository offlineShops,
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<WithdrawProxyShopEarningsService> logger) : IWithdrawProxyShopEarningsService
{
    private const short ProxyShopWithdrawEventCode = 4;

    private const int NothingToWithdrawErrorNumber = 50340;
    private const int MoneyCapOverflowErrorNumber = 50261;
    private const int BigMoneyCapOverflowErrorNumber = 50333;

    public async ValueTask<WithdrawProxyShopEarningsResponse> WithdrawAsync(int characterId, int accountId,
        int money, int bigMoney, CancellationToken cancellationToken)
    {
        try
        {
            await offlineShops.WithdrawMoneyAsync(characterId, money, bigMoney, GameDate.Today(),
                cancellationToken);
        }
        catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException { Number: NothingToWithdrawErrorNumber })
        {
            logger.LogInformation(
                "Character {CharacterId} offline-shop withdraw denied: nothing to withdraw", characterId);
            return new WithdrawProxyShopEarningsResponse { Result = 4, Money = 0, BigMoney = 0 };
        }
        catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException { Number: MoneyCapOverflowErrorNumber })
        {
            logger.LogInformation(
                "Character {CharacterId} offline-shop withdraw blocked: crediting {Money} would exceed the money cap",
                characterId, money);
            return new WithdrawProxyShopEarningsResponse { Result = 1, Money = 0, BigMoney = 0 };
        }
        catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException { Number: BigMoneyCapOverflowErrorNumber })
        {
            logger.LogInformation(
                "Character {CharacterId} offline-shop withdraw blocked: crediting {BigMoney} would exceed the BigMoney cap",
                characterId, bigMoney);
            return new WithdrawProxyShopEarningsResponse { Result = 2, Money = 0, BigMoney = 0 };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop withdraw WithdrawMoneyAsync failed", characterId);
            return new WithdrawProxyShopEarningsResponse { Result = 3, Money = 0, BigMoney = 0 };
        }

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

        return new WithdrawProxyShopEarningsResponse { Result = 0, Money = money, BigMoney = bigMoney };
    }
}
