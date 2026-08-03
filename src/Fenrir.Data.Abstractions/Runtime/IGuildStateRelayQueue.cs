namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildStateRelayQueue
{
    public bool Enqueue(GuildStateRelayEntry entry);
}
