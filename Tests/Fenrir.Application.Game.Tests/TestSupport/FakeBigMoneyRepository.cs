using Fenrir.Data.Abstractions.Inventory;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeBigMoneyRepository : IBigMoneyRepository
{
    public bool ThrowOnAdjust { get; set; }

    public (int CharacterId, int DeltaInventoryBigMoney, int DeltaStoreBigMoney, short? AuditEventCode,
        long? AuditFromDelta, long? AuditToDelta)? LastInventoryStore
    {
        get;
        private set;
    }

    public (int CharacterId, int DeltaInventoryBigMoney, int AccountId, int DeltaVaultBigMoney,
        short? AuditEventCode, long? AuditFromDelta, long? AuditToDelta)? LastInventorySave
    {
        get;
        private set;
    }

    public ValueTask AdjustInventoryStoreAsync(int characterId, int deltaInventoryBigMoney, int deltaStoreBigMoney,
        CancellationToken ct, short? auditEventCode = null, long? auditFromDelta = null, long? auditToDelta = null)
    {
        if (ThrowOnAdjust)
            throw new InvalidOperationException("Simulated SQL failure");

        LastInventoryStore = (characterId, deltaInventoryBigMoney, deltaStoreBigMoney, auditEventCode,
            auditFromDelta, auditToDelta);
        return ValueTask.CompletedTask;
    }

    public ValueTask AdjustInventorySaveAsync(int characterId, int deltaInventoryBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct, short? auditEventCode = null, long? auditFromDelta = null,
        long? auditToDelta = null)
    {
        if (ThrowOnAdjust)
            throw new InvalidOperationException("Simulated SQL failure");

        LastInventorySave = (characterId, deltaInventoryBigMoney, accountId, deltaVaultBigMoney, auditEventCode,
            auditFromDelta, auditToDelta);
        return ValueTask.CompletedTask;
    }
}
