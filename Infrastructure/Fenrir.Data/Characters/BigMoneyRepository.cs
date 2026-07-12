using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Characters;

public sealed record BigMoneyRepository(ICaeriusNetDbContext Db) : IBigMoneyRepository
{
    // Calls usp_Character_AdjustBigStoreMoney, NOT a "usp_Character_AdjustBigMoneyStore" (that name never
    // existed as a deployed procedure -- economy-hardening pass 2026-07-12 deleted the orphan duplicate .sql
    // file it used to point at, which had been written but never appended to Database/_manifest.txt). This
    // repository method itself currently has zero live callers under Application/ (the actually-consumed
    // tSort 241/244 path is Fenrir.Data.Inventory.BigMoneyRepository.AdjustInventoryStoreAsync, injected via
    // the Inventory-namespace IBigMoneyRepository), but is kept pointed at the real procedure so it is at
    // least correct if ever wired up, rather than left referencing a deleted file.
    public async ValueTask AdjustBigMoneyStoreAsync(int characterId, int deltaBigMoney, int deltaBigStoreMoney,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustBigStoreMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .AddParameter("DeltaBigStoreMoney", deltaBigStoreMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    // Parameter name fixed to "DeltaCharacterBigMoney" (economy-hardening pass 2026-07-12) to match
    // usp_AccountVault_TransferBigMoneyWithCharacter.sql's actual 2nd parameter, @DeltaCharacterBigMoney --
    // the previous "DeltaBigMoney" name would have thrown "Procedure ... expects parameter
    // '@DeltaCharacterBigMoney', which was not supplied" the moment this method was ever invoked. This
    // repository method has zero live callers under Application/ today (the actually-consumed tSort 242/245
    // path is Fenrir.Data.Inventory.BigMoneyRepository.AdjustInventorySaveAsync, which already used the
    // correct parameter name), but is fixed so it is at least correct if ever wired up.
    public async ValueTask AdjustBigMoneyBankAsync(int characterId, int deltaBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountVault_TransferBigMoneyWithCharacter", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaCharacterBigMoney", deltaBigMoney, SqlDbType.Int)
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
