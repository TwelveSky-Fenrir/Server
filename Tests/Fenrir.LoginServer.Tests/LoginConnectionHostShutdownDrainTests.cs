using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Hosting;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.LoginServer.Tests;

// Regression coverage for the Major db-worker-persistence-durability gap: LoginConnectionHost used to rely
// on the default (non-overridden) BackgroundService.StopAsync, which returns as soon as the accept loop
// itself exits -- while FenrirTcpListener<TSession> keeps every already-accepted connection's own
// OnAcceptedAsync (and its finally-block TearDownAccountSessionAsync/LogLoginSessionEndedAsync writes)
// running fully detached and unawaited. LoginConnectionHost now overrides StopAsync to track every
// in-flight connection and await its full teardown before returning -- an exact port of the fix already
// shipped for Fenrir.Application.Game.Hosting.GameConnectionHost. This test proves the override actually
// drains: it accepts one real loopback connection, marks it authenticated (so the finally block's
// account-session teardown + EventLog write both fire), and asserts that by the time StopAsync returns,
// both artificially-delayed fake-repository writes have already completed -- which is only possible if
// StopAsync genuinely awaited OnAcceptedAsync's own task rather than returning immediately.
public sealed class LoginConnectionHostShutdownDrainTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task StopAsync_AwaitsInFlightConnectionTeardownBeforeReturning()
    {
        using var testCts = new CancellationTokenSource(TestTimeout);
        var ct = testCts.Token;

        var port = ReserveEphemeralLoopbackPort();
        var registry = new SessionRegistry();
        var accountSessions = new DelayedAccountSessionRepository();
        var eventLog = new DelayedEventLogRepository();

        var host = new LoginConnectionHost(
            Options.Create(new LoginServerOptions { Port = port }),
            null!, // IFrameDispatcher: never invoked -- this test sends zero bytes over the accepted socket
            new SessionRateLimiter(),
            registry,
            new LoginCapacityState(),
            accountSessions,
            eventLog,
            new IpFloodGuard(int.MaxValue, int.MaxValue, static (_, _) => ValueTask.CompletedTask,
                new SessionRegistry()),
            NullLogger<LoginConnectionHost>.Instance);

        await host.StartAsync(ct);

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, ct);

            // Wait for OnAcceptedAsync to register the session (registry.Register runs before anything that
            // could block), then mark it authenticated so the finally block's teardown branch actually runs
            // (LoginConnectionHost skips TearDownAccountSessionAsync entirely for a session whose AccountId
            // was never set).
            await WaitUntilAsync(() => registry.Count == 1, ct);
            Assert.True(registry.TryGet(1, out var session));
            var loginSession = Assert.IsType<LoginClientSession>(session);
            loginSession.MarkAuthenticated(4242);

            // Cancels the internal stoppingToken (unblocking the connection's pending PipeReader.ReadAsync,
            // which is otherwise parked forever since this test never sends a byte) and then -- this is the
            // behavior under test -- awaits OnAcceptedAsync's own task, including its finally block's two
            // artificially-delayed fake-repository writes, before returning.
            await host.StopAsync(ct);

            Assert.True(accountSessions.TearDownCompleted,
                "StopAsync returned before TearDownAccountSessionAsync's delayed repository writes completed");
            Assert.True(eventLog.LogCompleted,
                "StopAsync returned before LogLoginSessionEndedAsync's delayed repository write completed");

            // The connection's own finally block also unregisters itself -- draining is full teardown, not
            // just "the delayed writes happened to fire".
            Assert.Equal(0, registry.Count);
        }
        finally
        {
            client.Dispose();
            host.Dispose();
        }
    }

    // Binds to port 0 to let the OS pick a free port, then releases it so LoginConnectionHost's own
    // FenrirTcpListener (which never reports back what it bound to) can bind that same number itself --
    // same pattern as SocketConnectionSmokeTests.ReserveEphemeralLoopbackPort.
    private static int ReserveEphemeralLoopbackPort()
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.LocalEndPoint!).Port;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(10, ct);
        }
    }

    // Artificially slow (but real-shaped) IAccountSessionRepository: the delay simulates a genuine SQL round
    // trip, so a StopAsync that returns WITHOUT actually awaiting this connection's teardown would race past
    // TearDownCompleted still being false -- exactly the bug this test guards against.
    private sealed class DelayedAccountSessionRepository : IAccountSessionRepository
    {
        public volatile bool TearDownCompleted;

        public ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public async ValueTask MarkTearingDownAsync(int accountId, AccountSessionServerKind serverKind,
            byte? shardId, Guid sessionToken, CancellationToken ct)
        {
            await Task.Delay(75, CancellationToken.None);
        }

        public async ValueTask ClearIfOwnerAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
            Guid sessionToken, CancellationToken ct)
        {
            await Task.Delay(75, CancellationToken.None);
            TearDownCompleted = true;
        }

        public ValueTask<ImmutableArray<KickedAccountDto>> RefreshAndGetKickedAsync(
            AccountSessionServerKind serverKind, byte? shardId, IReadOnlyCollection<int> accountIds,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<ImmutableArray<ReapedAccountSessionDto>> ReapStaleAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask<int> GetActiveSessionCountAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class DelayedEventLogRepository : IEventLogRepository
    {
        public volatile bool LogCompleted;

        public async ValueTask LogAsync(short eventCode, EventLogCategory category, int? actorAccountId,
            int? actorCharacterId, int? targetAccountId, int? targetCharacterId, short? shardId, long? deltaMoney,
            long? deltaBigMoney, int? itemId, int? quantity, byte? outcome, string? payload, CancellationToken ct)
        {
            await Task.Delay(75, CancellationToken.None);
            LogCompleted = true;
        }

        public ValueTask BatchLogAsync(IReadOnlyList<EventLogEntryTvp> rows, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}
