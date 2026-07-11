using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Inventory;

namespace Fenrir.Data.Inventory;

/// <inheritdoc cref="IBigMoneyRepository" />
public sealed record BigMoneyRepository(ICaeriusNetDbContext Db) : IBigMoneyRepository
{
    public async ValueTask AdjustInventoryStoreAsync(int characterId, int deltaInventoryBigMoney,
        int deltaStoreBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustBigStoreMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaBigMoney", deltaInventoryBigMoney, SqlDbType.Int)
            .AddParameter("DeltaBigStoreMoney", deltaStoreBigMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask AdjustInventorySaveAsync(int characterId, int deltaInventoryBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountVault_TransferBigMoneyWithCharacter", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaCharacterBigMoney", deltaInventoryBigMoney, SqlDbType.Int)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("DeltaVaultBigMoney", deltaVaultBigMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
