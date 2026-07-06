using Fenrir.Application.Login;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Hosting;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.LoginServer.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.LoginServer.Tests;

// C03: Greet() used to report SessionRegistry.Count (sockets currently mid-login on this one process) as the
// client-facing population figure. ReadLivePlayerCountAsync replaces it with the real CCU sum across every
// live shard in runtime.GameServerDirectory -- the same directory ZoneTransferHandler/ShardPartitionGuard
// already read for shard selection.
public class ReadLivePlayerCountAsyncTests
{
    [Fact]
    public async Task SumsCcuAcrossEveryLiveShard()
    {
        var directory = new FakeGameServerDirectoryRepository(
            new ShardDirectoryEntryDto(1, "10.0.0.1", 30001, 12, 100, 0f),
            new ShardDirectoryEntryDto(2, "10.0.0.2", 30002, 30, 100, 0f));
        var host = CreateHost(directory);

        var total = await host.ReadLivePlayerCountAsync(CancellationToken.None);

        Assert.Equal(42, total);
    }

    [Fact]
    public async Task NoLiveShards_ReturnsZero()
    {
        var host = CreateHost(new FakeGameServerDirectoryRepository());

        var total = await host.ReadLivePlayerCountAsync(CancellationToken.None);

        Assert.Equal(0, total);
    }

    [Fact]
    public async Task DirectoryReadFails_ReturnsZeroInsteadOfPropagating()
    {
        var host = CreateHost(new FailingGameServerDirectoryRepository());

        var total = await host.ReadLivePlayerCountAsync(CancellationToken.None);

        Assert.Equal(0, total);
    }

    private static LoginConnectionHost CreateHost(IGameServerDirectoryRepository directory)
    {
        // dispatcher/rateLimiter are never touched by ReadLivePlayerCountAsync -- only Greet()'s I/O-pump
        // continuation (not exercised here) would need them.
        return new LoginConnectionHost(
            Options.Create(new LoginServerOptions()),
            dispatcher: null!,
            rateLimiter: null!,
            new SessionRegistry(),
            directory,
            new FakeAccountSessionRepository(),
            NullLogger<LoginConnectionHost>.Instance);
    }
}
