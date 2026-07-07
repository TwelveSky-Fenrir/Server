using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_SET_DEPUTY_PSHOP_MONEY_SEND (opcode 110), extracted from
///     <see cref="WithdrawProxyShopEarningsHandler" />.
/// </summary>
public interface IWithdrawProxyShopEarningsService
{
    /// <summary>
    ///     Withdraws accumulated offline-shop earnings into the character's live money. Requires the shop
    ///     closed and the submitted amounts to still match current earnings (CAS guard in
    ///     <see cref="OfflineShopRepository.WithdrawMoneyAsync" />).
    /// </summary>
    /// <param name="accountId">
    ///     The withdrawing player's account id -- carried only for the game.EventLog audit row written once
    ///     persistence succeeds (legacy <c>GL_1002_PXSHOP_MONEY</c>); not used for any validation or
    ///     persistence decision.
    /// </param>
    public ValueTask<WithdrawProxyShopEarningsResponse> WithdrawAsync(int characterId, int accountId, int money,
        int bigMoney, CancellationToken cancellationToken);
}
