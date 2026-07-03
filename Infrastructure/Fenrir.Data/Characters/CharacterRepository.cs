using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Characters;

/// <summary>
///     game.Characters access (architecture reference §11.1-§11.3). Singleton, injected only with
///     ICaeriusNetDbContext -- no SqlDbType or builder ever leaks past this type; callers see typed ValueTasks only.
/// </summary>
public sealed record CharacterRepository(ICaeriusNetDbContext Db)
{
    /// <summary>Character-select list for the account. Capacity 3 = MAX_USER_AVATAR_NUM, the legacy 3-slot cap.</summary>
    public async ValueTask<ReadOnlyCollection<CharacterSummaryDto>> GetByAccountAsync(int accountId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetByAccount", 3)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<CharacterSummaryDto>(sp, ct);
    }

    /// <summary>Creates a character in the given slot; returns the new CharacterId (usp_Character_Create's scalar result).</summary>
    public async ValueTask<int> CreateAsync(
        int accountId,
        byte slot,
        string name,
        byte tribe,
        byte gender,
        byte headType,
        byte faceType,
        short mapId,
        float posX,
        float posY,
        float posZ,
        int life,
        int maxLife,
        int mana,
        int maxMana,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_Create", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .AddParameter("Name", name, SqlDbType.NVarChar)
            .AddParameter("Tribe", tribe, SqlDbType.TinyInt)
            .AddParameter("Gender", gender, SqlDbType.TinyInt)
            .AddParameter("HeadType", headType, SqlDbType.TinyInt)
            .AddParameter("FaceType", faceType, SqlDbType.TinyInt)
            .AddParameter("MapId", mapId, SqlDbType.SmallInt)
            .AddParameter("PosX", posX, SqlDbType.Real)
            .AddParameter("PosY", posY, SqlDbType.Real)
            .AddParameter("PosZ", posZ, SqlDbType.Real)
            .AddParameter("Life", life, SqlDbType.Int)
            .AddParameter("MaxLife", maxLife, SqlDbType.Int)
            .AddParameter("Mana", mana, SqlDbType.Int)
            .AddParameter("MaxMana", maxMana, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    /// <summary>Deletes the character occupying (AccountId, Slot) -- CL_DELETE_AVATAR_SEND's target (wire contract §4.4).</summary>
    public async ValueTask DeleteAsync(int accountId, byte slot, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_Delete", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Slot", slot, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     Full world-entry snapshot for the ZC_REGISTER_AVATAR_RECV/AVATAR_INFO path (wire contract §6.2); null if the
    ///     character vanished mid-flight.
    /// </summary>
    public async ValueTask<CharacterWorldEntryDto?> GetForWorldEntryAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetForWorldEntry", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterWorldEntryDto>(sp, ct);
    }

    /// <summary>
    ///     Write-behind position flush (architecture reference §10.5/§11.3); usp_Character_PersistBatch is idempotent on
    ///     FlushSequence, so a network retry never regresses a position.
    /// </summary>
    public async ValueTask PersistPositionsAsync(IReadOnlyList<CharacterPositionTvp> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return; // SQL Server rejects an empty TVP outright -- never build the call for nothing to flush

        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_PersistBatch", 0)
            .AddTvpParameter("Positions", rows)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     Everything world entry needs in ONE round trip: the five result sets of the A3-extended
    ///     usp_Character_GetForWorldEntry (character+progression+quest state, items, skills, hotkeys, buffs).
    ///     Null if the character vanished mid-flight (empty RS0). <see cref="GetForWorldEntryAsync" /> stays as the
    ///     cheap M1-prefix read; this is the full snapshot the AVATAR_INFO/PlayerRuntimeState build consumes.
    /// </summary>
    public async ValueTask<CharacterWorldEntryBundle?> GetWorldEntryBundleAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_GetForWorldEntry", 64)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        var (characters, items, skills, hotkeys, buffs) = await Db
            .QueryMultipleReadOnlyCollectionAsync<CharacterWorldSnapshotDto, CharacterItemSlotDto, CharacterSkillDto,
                CharacterHotkeyDto, CharacterBuffDto>(sp, ct);

        return characters.Count == 0
            ? null
            : new CharacterWorldEntryBundle(characters[0], items, skills, hotkeys, buffs);
    }

    /// <summary>
    ///     Whole-container replace of one character's item slots (usp_CharacterItems_ReplaceContainer, transactional
    ///     DELETE+INSERT -- D7 regime (b): item state never rides the lossy write-behind path). An EMPTY list is a
    ///     legal, deliberate "clear the container": the TVP parameter is simply omitted (a READONLY TVP defaults to an
    ///     empty table server-side), because ADO.NET rejects streaming a zero-row TVP outright.
    /// </summary>
    public async ValueTask ReplaceContainerAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_CharacterItems_ReplaceContainer", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    /// <summary>
    ///     Write-behind progression flush (D7 regime (a)) -- the progression twin of
    ///     <see cref="PersistPositionsAsync" />, idempotent on the same per-character FlushSequence, so replays of
    ///     either batch flavor never regress state.
    /// </summary>
    public async ValueTask PersistProgressAsync(IReadOnlyList<CharacterProgressTvp> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return; // SQL Server rejects an empty TVP outright -- never build the call for nothing to flush

        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_PersistProgressBatch", 0)
            .AddTvpParameter("Progress", rows)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     Atomic money adjustment with an overdraft guard (usp_Character_AdjustMoney, D7 regime (b)): throws
    ///     SQL error 50222 instead of clamping when either pool would go negative -- a caller relying on "the debit
    ///     happened" without checking must never silently under-pay.
    /// </summary>
    public async ValueTask AdjustMoneyAsync(int characterId, long deltaMoney, int deltaBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Character_AdjustMoney", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
