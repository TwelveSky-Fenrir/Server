using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Guilds;

public interface IGuildRepository
{
    public ValueTask<CharacterGuildMembershipDto?> GetByCharacterAsync(int characterId, CancellationToken ct);

    public ValueTask<GuildSummaryDto?> GetByIdAsync(int guildId, CancellationToken ct);

    /// <summary>
    ///     Every guild, master/buff/points included -- game.usp_Guild_GetAll (used by GuildBuffDecayHost's periodic
    ///     scan).
    /// </summary>
    public ValueTask<ReadOnlyCollection<GuildSummaryDto>> GetAllAsync(CancellationToken ct);

    /// <summary>Top <paramref name="count" /> guilds by Points, highest first -- game.usp_Guild_GetTopByPoints.</summary>
    public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetTopByPointsAsync(int count, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GuildRosterRowDto>> GetRosterAsync(int guildId, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GuildNoticeRowDto>> GetNoticesAsync(int guildId, CancellationToken ct);

    public ValueTask<int> CreateAsync(string name, int masterCharacterId, CancellationToken ct);

    /// <summary>
    ///     GUILD_WORK tSort 1 -- create the guild, enroll its master, and debit the creation cost in one
    ///     transaction (game.usp_Guild_CreateAndDebitMoney). Guild-tx-hygiene fix, not legacy parity: replaces the
    ///     separate <see cref="CreateAsync" /> + money-debit round trip that needed an app-level compensating
    ///     disband on debit failure. Throws SQL 50230 (name taken), 50231 (already in a guild), 50261 (money cap),
    ///     or 50277 (unknown character or insufficient balance). Returns the new GuildId.
    ///     Also writes one game.EventLog row (Category=GuildMoney, EventCode=1) in the same transaction --
    ///     see Database/Migrations/014_guild_money_event_log.sql.
    /// </summary>
    public ValueTask<int> CreateAndDebitMoneyAsync(string name, int masterCharacterId, long deltaMoney,
        int deltaBigMoney, CancellationToken ct);

    /// <summary>
    ///     GUILD_WORK tSort 6 -- delete a guild and everything hanging off it (game.usp_Guild_Disband).
    ///     <paramref name="characterId" /> is the acting (sole remaining) master, threaded through purely so the
    ///     procedure can attribute the guild-money audit row it now writes (Category=GuildMoney, EventCode=3,
    ///     DeltaMoney=0 -- see Database/Migrations/014_guild_money_event_log.sql) to the right character; it has
    ///     no bearing on the disband logic itself. Throws SQL 50235 if the guild is not found.
    /// </summary>
    public ValueTask DisbandAsync(int guildId, int characterId, CancellationToken ct);

    public ValueTask AddMemberAsync(int guildId, int characterId, CancellationToken ct);

    public ValueTask RemoveMemberAsync(int guildId, int characterId, CancellationToken ct);

    public ValueTask SetRoleAsync(int guildId, int characterId, byte role, CancellationToken ct);

    public ValueTask SetCallNameAsync(int guildId, int characterId, string callName, CancellationToken ct);

    public ValueTask SetMasterAsync(int guildId, int newMasterCharacterId, CancellationToken ct);

    public ValueTask SetLogoAsync(int guildId, int logo, CancellationToken ct);

    public ValueTask SetGradeAsync(int guildId, int grade, CancellationToken ct);

    /// <summary>
    ///     GUILD_WORK tSort 7 -- set the guild's grade and debit the character's upgrade cost in one transaction
    ///     (game.usp_Guild_UpgradeAndDebitMoney). Guild-tx-hygiene fix, not legacy parity: replaces the separate
    ///     <see cref="SetGradeAsync" /> + money-debit round trip that needed an app-level compensating grade
    ///     rollback on debit failure. Throws SQL 50235 (guild not found), 50261 (money cap), or 50278 (unknown
    ///     character or insufficient balance).
    ///     Also writes one game.EventLog row (Category=GuildMoney, EventCode=2) in the same transaction --
    ///     see Database/Migrations/014_guild_money_event_log.sql.
    /// </summary>
    public ValueTask UpgradeAndDebitMoneyAsync(int guildId, int grade, int characterId, long deltaMoney,
        int deltaBigMoney, CancellationToken ct);

    public ValueTask SetBuffAsync(int guildId, int buffType, int buffState, int buffTime, long buffTimeForDiff,
        CancellationToken ct);

    public ValueTask SetNoticeAsync(int guildId, byte noticeIndex, string text, CancellationToken ct);
}
