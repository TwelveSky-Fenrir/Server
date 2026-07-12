using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Tribes;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Tribes;

public sealed record TribeRepository(ICaeriusNetDbContext Db) : ITribeRepository
{
    // game.TribeBank is MEMORY_OPTIMIZED = ON: two characters depositing into the exact same never-used
    // (TribeId, SlotIndex) slot at the same instant can both see usp_TribeBank_Deposit's own UPDATE find zero
    // rows and both attempt the INSERT -- a natively compiled procedure can't take a table hint
    // (UPDLOCK/HOLDLOCK) to close this the way a disk-based upsert would, so the only correct remedy is a
    // bounded, no-backoff retry of the whole call. The loser surfaces either an immediate duplicate-key error
    // (2627/2601) or one of the SNAPSHOT-isolation conflict codes (41302/41305/41325 -- Microsoft Learn,
    // "Transactions with memory-optimized tables": a PRIMARY KEY/UNIQUE violation caused by a concurrent
    // transaction can itself surface as 41325, not only the immediate 2627), and the whole ambient
    // usp_TribeBank_DepositFromCharacter transaction rolls back under its own XACT_ABORT ON -- the character's
    // debited Money rolls back along with it, so retrying the whole call from scratch loses nothing. Mirrors
    // AccountSessionRepository's ClaimOrSignalKick-family retry for the SNAPSHOT codes, plus
    // usp_Character_ApplyTribeFourConversion's 2627/2601 catch for the same never-used-composite-key-race
    // shape -- see usp_TribeBank_Deposit.sql's own header comment.
    private const int MaxSlotInsertRaceAttempts = 3;

    private static bool IsTransientSlotInsertRaceConflict(int errorNumber)
    {
        return errorNumber is 2627 or 2601 or 41302 or 41305 or 41325;
    }

    public async ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeRole_GetForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<byte>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Tribe_GetAll", 4).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeSummaryDto>(sp, ct);
    }

    public async ValueTask SetMasterAsync(byte tribeId, int? newMasterCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Tribe_SetMaster", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("NewMasterCharacterId", (object?)newMasterCharacterId ?? DBNull.Value, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<TribeSubMasterDto>> GetSubMastersAsync(byte tribeId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeSubMaster_GetByTribe", 12)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeSubMasterDto>(sp, ct);
    }

    public async ValueTask SetSubMasterAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeSubMaster_Set", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask ClearSubMasterAsync(byte tribeId, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeSubMaster_Clear", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<TribeBankSlotDto>> GetBankAsync(byte tribeId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_GetByTribe", 50)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeBankSlotDto>(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<TribeBankTotalDto>> GetBankTotalsAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_GetTotals", 4).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeBankTotalDto>(sp, ct);
    }

    public async ValueTask<long> WithdrawBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_Withdraw", 1)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<long>(sp, ct);
    }

    public async ValueTask<long> DepositBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_DepositFromCharacter", 1)
                .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
                .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
                .AddParameter("CharacterId", characterId, SqlDbType.Int)
                .Build();

            try
            {
                return await Db.ExecuteScalarAsync<long>(sp, ct);
            }
            catch (SqlException ex) when (attempt < MaxSlotInsertRaceAttempts &&
                                           IsTransientSlotInsertRaceConflict(ex.Number))
            {
            }
        }
    }
}
