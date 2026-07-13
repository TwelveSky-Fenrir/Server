namespace Fenrir.Application.Game.Domain.Guilds;

public readonly record struct GuildMembershipZoneCommand(
    int CharacterId,
    int? GuildId,
    string GuildName,
    byte GuildRoleDb,
    string GuildCallName,
    TaskCompletionSource? Applied = null);
