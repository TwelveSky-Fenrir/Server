using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Data.Tribes;

// ReturnTribeRole (Server/Header/function.h:92-114) gates tribe announcements to master/sub-master; also the sub-master write surface (TRIBE_WORK tSort 2/3).
public sealed record TribeRepository(ICaeriusNetDbContext Db) : ITribeRepository
{
    /// <summary>
    ///     1 = tribe master, 2 = sub-master, 0 = regular member -- matches ReturnTribeRole's encoding directly, no
    ///     inversion (unlike the guild role enum).
    /// </summary>
    public async ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeRole_GetForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<byte>(sp, ct);
    }

    /// <summary>
    ///     All 4 tribes; TRIBE_WORK tSort 5's mWorldInfo-&gt;mTribePoint[i]&gt;100/ReturnSmallTribe gate reads every
    ///     tribe's Points at once.
    /// </summary>
    public async ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Tribe_GetAll", 4).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeSummaryDto>(sp, ct);
    }

    /// <summary>TRIBE_WORK tSort 55's tally write -- see <see cref="ITribeRepository.SetMasterAsync" />.</summary>
    public async ValueTask SetMasterAsync(byte tribeId, int? newMasterCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Tribe_SetMaster", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("NewMasterCharacterId", (object?)newMasterCharacterId ?? DBNull.Value, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>The up-to-12 occupied sub-master slots for one tribe (TRIBE_WORK tSort 2's free-slot/already-listed checks).</summary>
    public async ValueTask<ReadOnlyCollection<TribeSubMasterDto>> GetSubMastersAsync(byte tribeId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeSubMaster_GetByTribe", 12)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeSubMasterDto>(sp, ct);
    }

    /// <summary>TRIBE_WORK tSort 2 -- appoint one character to one (already-verified-free) sub-master slot.</summary>
    public async ValueTask SetSubMasterAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeSubMaster_Set", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>TRIBE_WORK tSort 3 -- remove one character's sub-master slot (idempotent).</summary>
    public async ValueTask ClearSubMasterAsync(byte tribeId, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeSubMaster_Clear", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>All 50 tribe-bank slot balances for one tribe (CZ_TRIBE_BANK_SEND sort 1 view and sort 2's balance read).</summary>
    public async ValueTask<ReadOnlyCollection<TribeBankSlotDto>> GetBankAsync(byte tribeId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_GetByTribe", 50)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeBankSlotDto>(sp, ct);
    }

    /// <summary>CZ_TRIBE_BANK_SEND sort 2; throws SQL 50210 (empty slot) or 50261 (would exceed the legacy money cap).</summary>
    public async ValueTask<long> WithdrawBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_Withdraw", 1)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<long>(sp, ct);
    }

    /// <summary>CZ_TRIBE_BANK_SEND sort 3 (Fenrir-only addition, see ITribeRepository.DepositBankAsync); throws SQL 50212 (nothing to deposit).</summary>
    public async ValueTask<long> DepositBankAsync(byte tribeId, byte slotIndex, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_DepositFromCharacter", 1)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<long>(sp, ct);
    }
}
