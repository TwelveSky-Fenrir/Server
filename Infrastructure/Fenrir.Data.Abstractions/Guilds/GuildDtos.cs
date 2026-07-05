using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Guilds;

/// <summary>
///     game.usp_GuildMember_GetByCharacter. Role is the DB-side enum (0 member, 1 sub-master, 2 master) -- INVERSE of
///     the legacy wire's aGuildRole; use <see cref="GuildRoleCodec" />, never compare raw.
/// </summary>
[GenerateDto]
public sealed partial record CharacterGuildMembershipDto(
    int GuildId,
    string GuildName,
    byte Role,
    string CallName);

// game.usp_Guild_GetById/usp_Guild_GetAll RS0. MemberCount is int, not long -- COUNT(*) returns INT, not BIGINT; long here throws InvalidCastException.
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

/// <summary>game.usp_GuildMember_GetByGuild, via game.vw_GuildRoster.</summary>
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

/// <summary>game.usp_Guild_GetTopByPoints RS0 -- one ranking-board row (Application.Game.Guilds.GuildRankingCache).</summary>
[GenerateDto]
public sealed partial record GuildRankingRowDto(
    int GuildId,
    string Name,
    int Points);
