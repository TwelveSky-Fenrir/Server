namespace Fenrir.Data.Abstractions.Guilds;

public interface IFourGuildKillPointQueue
{
    public bool Enqueue(int guildId);
}
