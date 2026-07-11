using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Data.Guilds;

public sealed record GuildRepository(ICaeriusNetDbContext Db) : IGuildRepository
{

        public async ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_GetByCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<CharacterGuildMembershipDto>(sp, ct);
    }

        public async ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_GetById", 1)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .Build();

        return await Db.FirstQueryAsync<GuildSummaryDto>(sp, ct);
    }

        public async ValueTask<ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_GetAll", 64).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildSummaryDto>(sp, ct);
    }

        public async ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(int count, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_GetTopByPoints", count)
            .AddParameter("Count", count, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildRankingRowDto>(sp, ct);
    }

        public async ValueTask AdjustPointsAsync(int guildId, int delta, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_AdjustPoints", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(int guildId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_GetByGuild", 50)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildRosterRowDto>(sp, ct);
    }

        public async ValueTask<ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(int guildId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildNotice_GetByGuild", 4)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildNoticeRowDto>(sp, ct);
    }

        public async ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_Create", 1)
            .AddParameter("Name", name, SqlDbType.NVarChar, 12)
            .AddParameter("MasterCharacterId", masterCharacterId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

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

        public async ValueTask DisbandAsync(int guildId, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_Disband", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask AddMemberAsync(int guildId, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_Add", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Role", (byte)0, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask RemoveMemberAsync(int guildId, int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_Remove", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask SetRoleAsync(int guildId, int characterId, byte role, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_SetRole", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Role", role, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask SetCallNameAsync(int guildId, int characterId, string callName, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_GuildMember_SetCallName", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("CallName", callName, SqlDbType.NVarChar, 4)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask SetMasterAsync(int guildId, int newMasterCharacterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_SetMaster", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("NewMasterCharacterId", newMasterCharacterId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask SetLogoAsync(int guildId, int logo, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_SetLogo", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("Logo", logo, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

        public async ValueTask SetGradeAsync(int guildId, int grade, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_SetGrade", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("Grade", grade, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

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
