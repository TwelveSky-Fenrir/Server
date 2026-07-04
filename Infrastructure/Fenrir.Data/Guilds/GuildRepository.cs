using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Guilds;

/// <summary>
///     game.Guilds/game.GuildMembers/game.GuildNotices access for the Server Logic chapter. Phase C/V6
///     Social added the READ-only slice this type started as ("does this character belong to a guild, and
///     what's its name/my role" for guild chat/announcement routing). Phase C/V7 Guilds &amp; Tribes adds
///     the full WRITE surface (create/disband/join/leave/kick/promote/title/transfer/logo/grade/buff/points
///     -- GUILD_WORK, doc 10 §1/contracts/06_guild_tribe.md) plus the two reads GuildActionHandler needs
///     that no earlier batch required (one guild's own full row, and its notices).
/// </summary>
public sealed record GuildRepository(ICaeriusNetDbContext Db) : IGuildRepository
{
    /// <summary>Loaded once at world entry (same "cache on PlayerRuntimeState, never re-query per chat message" posture as <c>MuteRepository</c>) -- null if the character belongs to no guild.</summary>
    public async ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_GetByCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterGuildMembershipDto>(sp, ct);
    }

    /// <summary>One guild's own row (GUILD_WORK tSort 2 and every other successful response's GUILD_INFO) -- null if the guild no longer exists (e.g. raced with a concurrent disband).</summary>
    public async ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_GetById", 1)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<GuildSummaryDto>(sp, ct);
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

    /// <summary>GUILD_WORK tSort 1 -- create a guild and enroll its master (Role=2) in one transaction. Returns the new GuildId.</summary>
    public async ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_Create", 1)
            .AddParameter("Name", name, SqlDbType.NVarChar, 12)
            .AddParameter("MasterCharacterId", masterCharacterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    /// <summary>GUILD_WORK tSort 6 -- delete a guild and everything hanging off it.</summary>
    public async ValueTask DisbandAsync(int guildId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_Disband", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
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

    /// <summary>GUILD_WORK tSort 9 (AGM promote 1 / demote 2, DB-side role 0/1 -- never 2, that is <see cref="SetMasterAsync" />'s job).</summary>
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

    /// <summary>GUILD_WORK tSort 17 -- transfer leadership: demotes the current master to Role=2 (member) and promotes the new one, keeping Guilds.MasterCharacterId consistent, all in one transaction.</summary>
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

    /// <summary>GUILD_WORK tSort 14 (buff type choice, USE_GUILD_BUFF) -- writes the whole BuffType/BuffState/BuffTime/BuffTimeForDiff block at once, matching the legacy's single UPDATE.</summary>
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
