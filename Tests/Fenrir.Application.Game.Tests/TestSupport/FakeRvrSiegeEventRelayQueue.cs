using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeRvrSiegeEventRelayQueue : IRvrSiegeEventRelayQueue
{
    public List<RvrSiegeEventRelayEntry> Enqueued { get; } = [];

    public bool Enqueue(RvrSiegeEventRelayEntry entry)
    {
        Enqueued.Add(entry);
        return true;
    }
}
