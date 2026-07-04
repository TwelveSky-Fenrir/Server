using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Guilds;

/// <summary>
///     A character's own guild membership (game.usp_GuildMember_GetByCharacter) -- ordinal contract:
///     GuildId, GuildName, Role, CallName. <see cref="Role" /> is the DB-side enum (0 member, 1 sub-master, 2
///     master, game.GuildMembers' own header) -- the INVERSE of the raw legacy wire's aGuildRole; see
///     <see cref="GuildRoleCodec" /> for the translation, never compare this raw value against a wire
///     constant directly. <see cref="CallName" /> added Phase C/V7 Guilds &amp; Tribes (GuildMembers_callname.sql).
/// </summary>
[GenerateDto]
public sealed partial record CharacterGuildMembershipDto(
    int GuildId,
    string GuildName,
    byte Role,
    string CallName);

/// <summary>
///     One guild's own row (game.usp_Guild_GetById/usp_Guild_GetAll) -- ordinal contract matching both
///     procs' identical RS0 shape (Phase C/V7 Guilds &amp; Tribes). <see cref="MemberCount" /> is <c>int</c>,
///     NOT <c>long</c> -- [CORRIGÉ-REVUE] verified against a real SQL Server container: plain T-SQL
///     <c>COUNT(*)</c> (game.vw_GuildRosterCounts) returns INT, not BIGINT (only <c>COUNT_BIG(*)</c> would);
///     an earlier pass here declared this <c>long</c> on the strength of the proc's own header comment
///     alone, which threw <c>InvalidCastException</c> the first time a real container actually ran it.
/// </summary>
[GenerateDto]
public sealed partial record GuildSummaryDto(
    int GuildId,
    string Name,
    int Grade,
    int? MasterCharacterId,
    int Points,
    int BuffType,
    int BuffState,
    int BuffTime,
    long BuffTimeForDiff,
    int Logo,
    DateTime CreatedAtUtc,
    int MemberCount);

/// <summary>
///     One roster row (game.usp_GuildMember_GetByGuild, via game.vw_GuildRoster) -- ordinal contract.
/// </summary>
[GenerateDto]
public sealed partial record GuildRosterRowDto(
    int GuildId,
    string GuildName,
    int CharacterId,
    string CharacterName,
    byte Role,
    string CallName,
    DateTime JoinedAtUtc);

/// <summary>One notice slot (game.usp_GuildNotice_GetByGuild) -- ordinal contract.</summary>
[GenerateDto]
public sealed partial record GuildNoticeRowDto(
    int GuildId,
    byte NoticeIndex,
    string Text,
    DateTime UpdatedAtUtc);
