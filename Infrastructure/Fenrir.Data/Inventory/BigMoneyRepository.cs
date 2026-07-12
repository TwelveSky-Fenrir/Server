using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Inventory;

namespace Fenrir.Data.Inventory;

public sealed record BigMoneyRepository(ICaeriusNetDbContext Db) : IBigMoneyRepository
{
    public async ValueTask AdjustInventoryStoreAsync(int characterId, int deltaInventoryBigMoney,
        int deltaStoreBigMoney, CancellationToken ct, short? auditEventCode = null, long? auditFromDelta = null,
        long? auditToDelta = null)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustBigStoreMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaBigMoney", deltaInventoryBigMoney, SqlDbType.Int)
            .AddParameter("DeltaBigStoreMoney", deltaStoreBigMoney, SqlDbType.Int)
            .AddParameter("AuditEventCode", (object?)auditEventCode ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter("AuditFromDelta", (object?)auditFromDelta ?? DBNull.Value, SqlDbType.BigInt)
            .AddParameter("AuditToDelta", (object?)auditToDelta ?? DBNull.Value, SqlDbType.BigInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask AdjustInventorySaveAsync(int characterId, int deltaInventoryBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct, short? auditEventCode = null, long? auditFromDelta = null,
        long? auditToDelta = null)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountVault_TransferBigMoneyWithCharacter", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaCharacterBigMoney", deltaInventoryBigMoney, SqlDbType.Int)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("DeltaVaultBigMoney", deltaVaultBigMoney, SqlDbType.Int)
            .AddParameter("AuditEventCode", (object?)auditEventCode ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter("AuditFromDelta", (object?)auditFromDelta ?? DBNull.Value, SqlDbType.BigInt)
            .AddParameter("AuditToDelta", (object?)auditToDelta ?? DBNull.Value, SqlDbType.BigInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
