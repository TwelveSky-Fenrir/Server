using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Commerce;

public interface ICashRepository
{
    public ValueTask<int> GetBalanceAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     Optional trailing <c>audit*</c> parameters nest a CashShopPurchase EventLog row into this same
    ///     procedure call (see usp_Cash_DebitAndGrantItem.sql) instead of a second, unshared round trip to
    ///     <c>IEventLogRepository.LogAsync</c> afterward (transaction-composition-audit finding). Omit
    ///     <paramref name="auditItemId" /> to skip logging.
    /// </summary>
    public ValueTask<int> DebitAndGrantItemAsync(int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct,
        int? auditItemId = null, int? auditQuantity = null, int? auditSerial = null);

    public ValueTask CreditAsync(int accountId, int amount, byte reason, int? productId, CancellationToken ct);

    public ValueTask<int> CreditAndConsumeItemAsync(int accountId, int amount, byte reason, int? productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);
}
