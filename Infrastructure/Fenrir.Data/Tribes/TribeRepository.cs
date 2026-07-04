using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Tribes;

/// <summary>
///     game.Tribes/game.TribeSubMasters access. Phase C/V6 Social added the READ-only slice this type
///     started as -- specifically <c>ReturnTribeRole</c> (verified in full, <c>Server/Header/function.h:92-114</c>)
///     for tribe announcement gating (master/sub-master only) and the ZC_TRIBE_NOTICE_RECV TribeRole wire
///     field. Phase C/V7 Guilds &amp; Tribes adds the sub-master WRITE surface (TRIBE_WORK tSort 2/3, doc
///     10 §2) plus the read GetAllAsync needs for TRIBE_WORK's own gates (tSort 5's per-tribe point check).
/// </summary>
public sealed record TribeRepository(ICaeriusNetDbContext Db) : ITribeRepository
{
    /// <summary>
    ///     Loaded once at world entry, same posture as <c>MuteRepository</c>/<c>GuildRepository</c>. 1 =
    ///     tribe master, 2 = sub-master, 0 = regular member (matches <c>ReturnTribeRole</c>'s own
    ///     encoding directly -- no inversion, unlike the guild role enum).
    /// </summary>
    public async ValueTask<byte> GetRoleForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeRole_GetForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<byte>(sp, ct);
    }

    /// <summary>
    ///     All 4 tribes (MAX_TRIBE_NUM) -- TRIBE_WORK tSort 5's own <c>mWorldInfo-&gt;mTribePoint[i]&gt;100</c>/
    ///     <c>ReturnSmallTribe</c> gate reads every tribe's Points at once.
    /// </summary>
    public async ValueTask<ReadOnlyCollection<TribeSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Tribe_GetAll", 4).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeSummaryDto>(sp, ct);
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

    /// <summary>
    ///     All 50 tribe-bank slot balances for one tribe (CZ_TRIBE_BANK_SEND sort 1 view, and sort 2's own
    ///     current-balance read).
    /// </summary>
    public async ValueTask<ReadOnlyCollection<TribeBankSlotDto>> GetBankAsync(byte tribeId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_GetByTribe", 50)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<TribeBankSlotDto>(sp, ct);
    }

    /// <summary>
    ///     CZ_TRIBE_BANK_SEND sort 2 -- withdraw the given amount from one slot; throws (50210/50211) if the amount is
    ///     non-positive or exceeds the slot's balance.
    /// </summary>
    public async ValueTask WithdrawBankAsync(byte tribeId, byte slotIndex, int amount, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeBank_Withdraw", 0)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("Amount", amount, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
