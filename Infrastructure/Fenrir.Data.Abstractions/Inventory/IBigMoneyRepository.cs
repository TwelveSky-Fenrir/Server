namespace Fenrir.Data.Abstractions.Inventory;

public interface IBigMoneyRepository
{

        public ValueTask AdjustInventoryStoreAsync(int characterId, int deltaInventoryBigMoney,
        int deltaStoreBigMoney, CancellationToken ct, short? auditEventCode = null, long? auditFromDelta = null,
        long? auditToDelta = null);

        public ValueTask AdjustInventorySaveAsync(int characterId, int deltaInventoryBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct, short? auditEventCode = null, long? auditFromDelta = null,
        long? auditToDelta = null);
}
