namespace Fenrir.Data.Abstractions.Runtime;

public interface IRvrSiegeEventRelayQueue
{

        public bool Enqueue(RvrSiegeEventRelayEntry entry);
}
