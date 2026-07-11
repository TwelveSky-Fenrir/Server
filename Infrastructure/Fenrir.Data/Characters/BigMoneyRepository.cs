using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record BigMoneyRepository(ICaeriusNetDbContext Db) : IBigMoneyRepository
{
    public async ValueTask AdjustBigMoneyStoreAsync(int characterId, int deltaBigMoney, int deltaBigStoreMoney,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustBigMoneyStore", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .AddParameter("DeltaBigStoreMoney", deltaBigStoreMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask AdjustBigMoneyBankAsync(int characterId, int deltaBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountVault_TransferBigMoneyWithCharacter", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("DeltaVaultBigMoney", deltaVaultBigMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask AdjustBigMoneyConversionAsync(int characterId, long deltaMoney, int deltaBigMoney,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustBigMoneyConversion", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
