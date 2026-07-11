using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Hosting;
using Fenrir.Application.Login.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests;

public class ServerQuotaRefreshHostTests
{
    [Fact]
    public async Task InitializeAsync_PopulatesMaxPlayersFromTheDurableStore()
    {
        var state = new LoginCapacityState();
        var quota = new FakeServerQuotaRepository { MaxPlayers = 500 };
        var host = CreateHost(state, quota, new FakeAccountSessionRepository());

        await host.InitializeAsync(CancellationToken.None);

        Assert.Equal(500, state.MaxPlayers);
        Assert.Equal(1, quota.CallCount);
    }

    [Fact]
    public async Task InitializeAsync_DurableStoreReadFails_PropagatesTheException_FatalStartupError()
    {
        var state = new LoginCapacityState();
        var quota = new FakeServerQuotaRepository { Exception = new InvalidOperationException("boom") };
        var host = CreateHost(state, quota, new FakeAccountSessionRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.InitializeAsync(CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task RefreshOnceAsync_MaxPlayersPositive_RefreshesBothMaxAndCurrentPlayers()
    {
        var state = new LoginCapacityState();
        var quota = new FakeServerQuotaRepository { MaxPlayers = 1000 };
        var accountSessions = new FakeAccountSessionRepository { ActiveSessionCount = 42 };
        var host = CreateHost(state, quota, accountSessions);

        await host.RefreshOnceAsync(CancellationToken.None);

        Assert.Equal(1000, state.MaxPlayers);
        Assert.Equal(42, state.CurrentPlayers);
        Assert.Equal(1, accountSessions.ActiveSessionCountCallCount);
    }

    [Fact]
    public async Task RefreshOnceAsync_MaxPlayersIsZero_SkipsTheCurrentCountRefreshEntirely()
    {
        var state = new LoginCapacityState();
        var quota = new FakeServerQuotaRepository { MaxPlayers = 0 };
        var accountSessions = new FakeAccountSessionRepository { ActiveSessionCount = 42 };
        var host = CreateHost(state, quota, accountSessions);

        await host.RefreshOnceAsync(CancellationToken.None);

        Assert.Equal(0, state.MaxPlayers);
        Assert.Equal(0, state.CurrentPlayers);
        Assert.Equal(0, accountSessions.ActiveSessionCountCallCount);
    }

    [Fact]
    public async Task RefreshOnceAsync_MaxPlayersReadFails_KeepsThePreviousMaxPlayers_AndDoesNotThrow()
    {
        var state = new LoginCapacityState();
        state.SetMaxPlayers(750);
        var quota = new FakeServerQuotaRepository { Exception = new InvalidOperationException("boom") };
        var accountSessions = new FakeAccountSessionRepository { ActiveSessionCount = 42 };
        var host = CreateHost(state, quota, accountSessions);

        await host.RefreshOnceAsync(CancellationToken.None);

        Assert.Equal(750, state.MaxPlayers);
        Assert.Equal(1, accountSessions.ActiveSessionCountCallCount);
        Assert.Equal(42, state.CurrentPlayers);
    }

    [Fact]
    public async Task RefreshOnceAsync_CurrentCountReadFails_KeepsThePreviousCurrentPlayers_AndDoesNotThrow()
    {
        var state = new LoginCapacityState();
        state.SetCurrentPlayers(13);
        var quota = new FakeServerQuotaRepository { MaxPlayers = 1000 };
        var accountSessions = new FakeAccountSessionRepository
            { ActiveSessionCountException = new InvalidOperationException("boom") };
        var host = CreateHost(state, quota, accountSessions);

        await host.RefreshOnceAsync(CancellationToken.None);

        Assert.Equal(1000, state.MaxPlayers);
        Assert.Equal(13, state.CurrentPlayers);
    }

    private static ServerQuotaRefreshHost CreateHost(LoginCapacityState state, FakeServerQuotaRepository quota,
        FakeAccountSessionRepository accountSessions)
    {
        return new ServerQuotaRefreshHost(state, quota, accountSessions,
            NullLogger<ServerQuotaRefreshHost>.Instance);
    }
}
