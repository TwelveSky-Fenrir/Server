using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_SET_DEPUTY_PSHOP_MONEY_SEND (opcode 110), extracted from
///     <see cref="WithdrawProxyShopEarningsHandler" />.
/// </summary>
public interface IWithdrawProxyShopEarningsService
{
    /// <summary>
    ///     Withdraws accumulated offline-shop earnings into the character's live money. Requires the shop
    ///     closed, not expired, and the submitted amounts to still match current earnings (CAS guard in
    ///     <see cref="OfflineShopRepository.WithdrawMoneyAsync" />). Result 4 ("nothing to withdraw") is
    ///     returned when both submitted amounts are zero, distinct from result 3 (stale-client mismatch,
    ///     shop not closed, or shop expired).
    /// </summary>
    /// <param name="accountId">
    ///     The withdrawing player's account id -- carried only for the game.EventLog audit row written once
    ///     persistence succeeds (legacy <c>GL_1002_PXSHOP_MONEY</c>); not used for any validation or
    ///     persistence decision.
    /// </param>
    public ValueTask<WithdrawProxyShopEarningsResponse> WithdrawAsync(int characterId, int accountId, int money,
        int bigMoney, CancellationToken cancellationToken);
}
