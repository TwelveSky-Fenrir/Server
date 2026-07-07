using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Data.Guilds;

// game.Guilds/GuildMembers/GuildNotices access; the write surface backs GUILD_WORK (create/disband/join/leave/kick/promote/title/transfer/logo/grade/buff).
public sealed record GuildRepository(ICaeriusNetDbContext Db) : IGuildRepository
{
    /// <summary>Loaded once at world entry, never re-queried per chat message; null if the character belongs to no guild.</summary>
    public async ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_GetByCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterGuildMembershipDto>(sp, ct);
    }

    /// <summary>GUILD_WORK tSort 2 / GUILD_INFO; null if the guild no longer exists (e.g. raced with a concurrent disband).</summary>
    public async ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_GetById", 1)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<GuildSummaryDto>(sp, ct);
    }

    /// <summary>Every guild -- game.usp_Guild_GetAll, the same RS0 shape as <see cref="GetByIdAsync" />.</summary>
    public async ValueTask<ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_GetAll", 64).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildSummaryDto>(sp, ct);
    }

    /// <summary>Ranking-board top N by Points, highest first -- game.usp_Guild_GetTopByPoints.</summary>
    public async ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(int count, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_GetTopByPoints", count)
            .AddParameter("Count", count, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildRankingRowDto>(sp, ct);
    }

    /// <summary>
    ///     Guild-point counter delta (e.g. the RvR four-guild-event enemy-tribe-kill credit) -- game.usp_Guild_AdjustPoints.
    ///     See <see cref="IGuildRepository.AdjustPointsAsync" /> for the legacy citation and the gating this
    ///     method deliberately leaves to the caller.
    /// </summary>
    public async ValueTask AdjustPointsAsync(int guildId, int delta, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_AdjustPoints", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>Full roster for one guild, master/sub-master first (GUILD_INFO.MemberNames/MemberRoles/MemberCallNames).</summary>
    public async ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(int guildId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_GetByGuild", 50)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildRosterRowDto>(sp, ct);
    }

    /// <summary>The (0-4) notice slots (GUILD_INFO.Notices) -- GUILD_NOTICE_V2's .DAT replacement.</summary>
    public async ValueTask<ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(int guildId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildNotice_GetByGuild", 4)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildNoticeRowDto>(sp, ct);
    }

    /// <summary>
    ///     GUILD_WORK tSort 1 -- create a guild and enroll its master (Role=2) in one transaction. Returns the new
    ///     GuildId.
    /// </summary>
    public async ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_Create", 1)
            .AddParameter("Name", name, SqlDbType.NVarChar, 12)
            .AddParameter("MasterCharacterId", masterCharacterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    /// <summary>
    ///     GUILD_WORK tSort 1 -- create the guild, enroll its master, and debit the creation cost atomically
    ///     (game.usp_Guild_CreateAndDebitMoney). No caller-side compensation needed: a failed debit means the
    ///     whole transaction, including the guild row, never commits. Returns the new GuildId.
    /// </summary>
    public async ValueTask<int> CreateAndDebitMoneyAsync(string name, int masterCharacterId, long deltaMoney,
        int deltaBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_CreateAndDebitMoney", 1)
            .AddParameter("Name", name, SqlDbType.NVarChar, 12)
            .AddParameter("MasterCharacterId", masterCharacterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    /// <summary>
    ///     GUILD_WORK tSort 6 -- delete a guild and everything hanging off it, plus one guild-money audit row
    ///     (see game.usp_Guild_Disband's own doc comment / Database/Migrations/014_guild_money_event_log.sql).
    /// </summary>
    public async ValueTask DisbandAsync(int guildId, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_Disband", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>GUILD_WORK tSort 3 (invite finalize) -- enroll one character (Role=0 member).</summary>
    public async ValueTask AddMemberAsync(int guildId, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_Add", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Role", (byte)0, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>GUILD_WORK tSort 4/8 (leave/kick) -- idempotent row deletion; who initiated it is the caller's business.</summary>
    public async ValueTask RemoveMemberAsync(int guildId, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_Remove", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>GUILD_WORK tSort 9 (promote/demote); DB-side role 0/1 only -- role 2 is <see cref="SetMasterAsync" />'s job.</summary>
    public async ValueTask SetRoleAsync(int guildId, int characterId, byte role, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_SetRole", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Role", role, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>GUILD_WORK tSort 10 (member title/CallName).</summary>
    public async ValueTask SetCallNameAsync(int guildId, int characterId, string callName, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_SetCallName", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("CallName", callName, SqlDbType.NVarChar, 4)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     GUILD_WORK tSort 17 -- transfer leadership: demotes the current master to Role=2, promotes the new one, keeps
    ///     MasterCharacterId consistent, one transaction.
    /// </summary>
    public async ValueTask SetMasterAsync(int guildId, int newMasterCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_SetMaster", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("NewMasterCharacterId", newMasterCharacterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>GUILD_WORK tSort 1001 (USE_GUILD_LOGO).</summary>
    public async ValueTask SetLogoAsync(int guildId, int logo, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_SetLogo", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("Logo", logo, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>GUILD_WORK tSort 7 (grade upgrade).</summary>
    public async ValueTask SetGradeAsync(int guildId, int grade, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_SetGrade", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("Grade", grade, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     GUILD_WORK tSort 7 -- set the guild's grade and debit the character's upgrade cost atomically
    ///     (game.usp_Guild_UpgradeAndDebitMoney). No caller-side compensation needed: a failed debit means the
    ///     grade update never commits either.
    /// </summary>
    public async ValueTask UpgradeAndDebitMoneyAsync(int guildId, int grade, int characterId, long deltaMoney,
        int deltaBigMoney, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_UpgradeAndDebitMoney", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("Grade", grade, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaMoney", deltaMoney, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", deltaBigMoney, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>
    ///     GUILD_WORK tSort 14 (USE_GUILD_BUFF) -- writes the whole buff block at once, matching the legacy's single
    ///     UPDATE.
    /// </summary>
    public async ValueTask SetBuffAsync(int guildId, int buffType, int buffState, int buffTime,
        long buffTimeForDiff, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_SetBuff", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("BuffType", buffType, SqlDbType.Int)
            .AddParameter("BuffState", buffState, SqlDbType.Int)
            .AddParameter("BuffTime", buffTime, SqlDbType.Int)
            .AddParameter("BuffTimeForDiff", buffTimeForDiff, SqlDbType.BigInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>Guild notice slot upsert (GUILD_WORK tSort 5, GUILD_NOTICE_V2).</summary>
    public async ValueTask SetNoticeAsync(int guildId, byte noticeIndex, string text, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildNotice_Set", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("NoticeIndex", noticeIndex, SqlDbType.TinyInt)
            .AddParameter("Text", text, SqlDbType.NVarChar, 50)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
