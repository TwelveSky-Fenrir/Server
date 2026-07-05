using System.Collections.Immutable;
using Fenrir.Data.Runtime;

namespace Fenrir.LoginServer.Tests.TestSupport;

// Simulates the directory's backing store (runtime.GameServerDirectory) being unreachable -- e.g. a transient
// SQL Server outage -- so tests can assert the login greet degrades to 0 rather than tearing down the session.
internal sealed class FailingGameServerDirectoryRepository : IGameServerDirectoryRepository
{
    public ValueTask<ImmutableArray<ShardDirectoryEntryDto>> GetDirectoryAsync(CancellationToken ct)
    {
        throw new InvalidOperationException("Simulated runtime.GameServerDirectory outage.");
    }

    public ValueTask HeartbeatAsync(byte shardId, string host, int port, int ccu, int capacity, float tickP99Ms,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
