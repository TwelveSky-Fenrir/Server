namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildTribeBroadcastRelayQueue
{

        public bool Enqueue(GuildTribeBroadcastRelayEntry entry);
}
