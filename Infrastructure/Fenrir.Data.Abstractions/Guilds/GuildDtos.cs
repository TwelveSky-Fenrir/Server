using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Guilds;

[GenerateDto]
public sealed partial record CharacterGuildMembershipDto(
    int GuildId,
    string GuildName,
    byte Role,
    string CallName);

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

[GenerateDto]
public sealed partial record GuildRosterRowDto(
    int GuildId,
    string GuildName,
    int CharacterId,
    string CharacterName,
    byte Role,
    string CallName,
    DateTime JoinedAtUtc);

[GenerateDto]
public sealed partial record GuildNoticeRowDto(
    int GuildId,
    byte NoticeIndex,
    string Text,
    DateTime UpdatedAtUtc);

[GenerateDto]
public sealed partial record GuildRankingRowDto(
    int GuildId,
    string Name,
    int Points);
