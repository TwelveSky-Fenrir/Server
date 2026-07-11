using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Hosting;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.LoginServer.Tests;

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
            null!,
            null!,
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

            await WaitUntilAsync(() => registry.Count == 1, ct);
            Assert.True(registry.TryGet(1, out var session));
            var loginSession = Assert.IsType<LoginClientSession>(session);
            loginSession.MarkAuthenticated(4242);

            await host.StopAsync(ct);

            Assert.True(accountSessions.TearDownCompleted,
                "StopAsync returned before TearDownAccountSessionAsync's delayed repository writes completed");
            Assert.True(eventLog.LogCompleted,
                "StopAsync returned before LogLoginSessionEndedAsync's delayed repository write completed");

            Assert.Equal(0, registry.Count);
        }
        finally
        {
            client.Dispose();
            host.Dispose();
        }
    }

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
