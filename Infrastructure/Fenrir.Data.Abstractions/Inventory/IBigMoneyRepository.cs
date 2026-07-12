namespace Fenrir.Data.Abstractions.Inventory;

public interface IBigMoneyRepository
{
    /// <summary>
    ///     Optional trailing <c>audit*</c> parameters nest a BigMoneyConversion audit row into this same
    ///     procedure call (see usp_Character_AdjustBigStoreMoney.sql) instead of the caller making a second,
    ///     unshared round trip to <c>IEventLogRepository.LogBigMoneyConversionAsync</c> afterward
    ///     (transaction-composition-audit finding). <paramref name="auditFromDelta" />/<paramref name="auditToDelta" />
    ///     mirror that method's own two-independent-longs shape. Omit <paramref name="auditEventCode" /> to skip
    ///     logging.
    /// </summary>
    public ValueTask AdjustInventoryStoreAsync(int characterId, int deltaInventoryBigMoney,
        int deltaStoreBigMoney, CancellationToken ct, short? auditEventCode = null, long? auditFromDelta = null,
        long? auditToDelta = null);

    /// <summary>
    ///     Optional trailing <c>audit*</c> parameters nest a BigMoneyConversion audit row into this same
    ///     procedure call (see usp_AccountVault_TransferBigMoneyWithCharacter.sql) instead of the caller making a
    ///     second, unshared round trip to <c>IEventLogRepository.LogBigMoneyConversionAsync</c> afterward
    ///     (transaction-composition-audit finding). Omit <paramref name="auditEventCode" /> to skip logging.
    /// </summary>
    public ValueTask AdjustInventorySaveAsync(int characterId, int deltaInventoryBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct, short? auditEventCode = null, long? auditFromDelta = null,
        long? auditToDelta = null);
}
