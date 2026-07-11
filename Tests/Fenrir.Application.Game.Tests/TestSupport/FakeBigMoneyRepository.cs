using Fenrir.Data.Abstractions.Inventory;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeBigMoneyRepository : IBigMoneyRepository
{
    public bool ThrowOnAdjust { get; set; }

    public (int CharacterId, int DeltaInventoryBigMoney, int DeltaStoreBigMoney)? LastInventoryStore
    {
        get;
        private set;
    }

    public (int CharacterId, int DeltaInventoryBigMoney, int AccountId, int DeltaVaultBigMoney)? LastInventorySave
    {
        get;
        private set;
    }

    public ValueTask AdjustInventoryStoreAsync(int characterId, int deltaInventoryBigMoney, int deltaStoreBigMoney,
        CancellationToken ct)
    {
        if (ThrowOnAdjust)
            throw new InvalidOperationException("Simulated SQL failure");

        LastInventoryStore = (characterId, deltaInventoryBigMoney, deltaStoreBigMoney);
        return ValueTask.CompletedTask;
    }

    public ValueTask AdjustInventorySaveAsync(int characterId, int deltaInventoryBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct)
    {
        if (ThrowOnAdjust)
            throw new InvalidOperationException("Simulated SQL failure");

        LastInventorySave = (characterId, deltaInventoryBigMoney, accountId, deltaVaultBigMoney);
        return ValueTask.CompletedTask;
    }
}
