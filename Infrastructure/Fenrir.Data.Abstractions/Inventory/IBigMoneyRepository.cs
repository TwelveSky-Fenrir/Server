namespace Fenrir.Data.Abstractions.Inventory;

public interface IBigMoneyRepository
{
    public ValueTask AdjustInventoryStoreAsync(int characterId, int deltaInventoryBigMoney,
        int deltaStoreBigMoney, CancellationToken ct);

    public ValueTask AdjustInventorySaveAsync(int characterId, int deltaInventoryBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct);
}
