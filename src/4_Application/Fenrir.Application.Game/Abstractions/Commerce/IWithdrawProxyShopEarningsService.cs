using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IWithdrawProxyShopEarningsService
{
    public ValueTask<WithdrawProxyShopEarningsResponse> WithdrawAsync(int characterId, int accountId, int money,
        int bigMoney, CancellationToken cancellationToken);
}
