namespace Fenrir.Data.Abstractions.Runtime;

public interface IGuildBuffExpiryRelayQueue
{

        public bool Enqueue(GuildBuffExpiryRelayEntry entry);
}
