using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

public enum GuildStateRelayKind : byte
{
    MembershipRemoved = 0,
    MembershipRoleChanged = 1,
    MembershipTitleChanged = 2,
    BuffActivation = 3
}

public sealed record GuildStateRelayEntry(
    GuildStateRelayKind Kind,
    byte SourceShardId,
    int GuildId,
    int? TargetCharacterId,
    int? NewGuildId,
    string GuildName,
    byte GuildRoleDb,
    string GuildCallName,
    int BuffType,
    bool BuffActive)
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

[GenerateDto]
public sealed partial record GuildStateRelayDto(
    long RelayId,
    byte Kind,
    int GuildId,
    int? TargetCharacterId,
    int? NewGuildId,
    string GuildName,
    byte GuildRoleDb,
    string GuildCallName,
    int BuffType,
    bool BuffActive);
