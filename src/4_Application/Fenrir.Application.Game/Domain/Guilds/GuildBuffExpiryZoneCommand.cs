namespace Fenrir.Application.Game.Domain.Guilds;

public readonly record struct GuildBuffExpiryZoneCommand(int GuildId, int NewBuffTime);
