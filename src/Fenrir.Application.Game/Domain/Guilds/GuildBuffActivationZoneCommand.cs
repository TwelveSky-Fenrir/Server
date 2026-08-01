namespace Fenrir.Application.Game.Domain.Guilds;

public readonly record struct GuildBuffActivationZoneCommand(int GuildId, int BuffType, bool BuffActive);
