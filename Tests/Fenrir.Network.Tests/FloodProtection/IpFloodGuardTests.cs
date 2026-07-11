using System.Net;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Tests.Sessions;

namespace Fenrir.Network.Tests.FloodProtection;

public sealed class IpFloodGuardTests
{
    private const string Ip = "203.0.113.10";

    [Fact]
    public async Task TryAcquireConnectionAsync_UpToThreshold_ReturnsTrueAndNeverBlocks()
    {
        var blockCalls = new List<string>();
        var guard = new IpFloodGuard(3, 30, RecordingBlockDelegate(blockCalls), new SessionRegistry());

        for (var i = 0; i < 3; i++)
            Assert.True(await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None));

        Assert.Empty(blockCalls);
    }

    [Fact]
    public async Task TryAcquireConnectionAsync_ExceedsThreshold_ReturnsFalseAndBlocksExactlyOnce()
    {
        var blockCalls = new List<string>();
        var guard = new IpFloodGuard(3, 30, RecordingBlockDelegate(blockCalls), new SessionRegistry());

        for (var i = 0; i < 3; i++)
            Assert.True(await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None));

        Assert.False(await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None));

        Assert.Equal([Ip], blockCalls);
    }

    [Fact]
    public async Task TryAcquireConnectionAsync_OverThreshold_KicksEverySessionSharingThatIpButNotOthers()
    {
        var registry = new SessionRegistry();
        var sameIpEndPoint = new IPEndPoint(IPAddress.Parse(Ip), 1);
        var sessionA = new ZoneClientSession(1, new FakeDuplexPipe(), sameIpEndPoint);
        var sessionB = new ZoneClientSession(2, new FakeDuplexPipe(), sameIpEndPoint);
        var otherIpSession =
            new ZoneClientSession(3, new FakeDuplexPipe(), new IPEndPoint(IPAddress.Parse("203.0.113.99"), 1));
        registry.Register(sessionA);
        registry.Register(sessionB);
        registry.Register(otherIpSession);

        var guard = new IpFloodGuard(1, 30, static (_, _) => ValueTask.CompletedTask, registry);

        Assert.True(await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None));
        Assert.False(await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None));

        Assert.Equal(DisconnectReason.IpBlocked, sessionA.DisconnectReason);
        Assert.Equal(DisconnectReason.IpBlocked, sessionB.DisconnectReason);
        Assert.Null(otherIpSession.DisconnectReason);
    }

    [Fact]
    public async Task ReleaseConnection_DecrementsGauge_AllowingAnotherAcquisitionUnderThreshold()
    {
        var guard = new IpFloodGuard(2, 30, static (_, _) => ValueTask.CompletedTask, new SessionRegistry());

        Assert.True(await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None));
        Assert.True(await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None));

        guard.ReleaseConnection(Ip);

        Assert.True(await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None));
    }

    [Fact]
    public void ReleaseConnection_NeverAcquired_IsABenignNoOp()
    {
        var guard = new IpFloodGuard(2, 30, static (_, _) => ValueTask.CompletedTask, new SessionRegistry());

        var exception = Record.Exception(() => guard.ReleaseConnection(Ip));

        Assert.Null(exception);
    }

    [Fact]
    public async Task TryAcquireConnectionAsync_DifferentIps_TrackedIndependently()
    {
        var guard = new IpFloodGuard(1, 30, static (_, _) => ValueTask.CompletedTask, new SessionRegistry());

        Assert.True(await guard.TryAcquireConnectionAsync("203.0.113.1", CancellationToken.None));
        Assert.True(await guard.TryAcquireConnectionAsync("203.0.113.2", CancellationToken.None));
    }

    [Fact]
    public async Task TryAcquireConnectionAsync_BlockDelegateThrows_StillReturnsFalseAndNeverThrows()
    {
        var guard = new IpFloodGuard(0, 30,
            static (_, _) => throw new InvalidOperationException("db down"),
            new SessionRegistry());

        var result = await guard.TryAcquireConnectionAsync(Ip, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task RecordProtocolViolationAsync_BelowThreshold_DoesNotBlock()
    {
        var blockCalls = new List<string>();
        var guard = new IpFloodGuard(40, 3, RecordingBlockDelegate(blockCalls), new SessionRegistry());

        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);
        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);

        Assert.Empty(blockCalls);
    }

    [Fact]
    public async Task RecordProtocolViolationAsync_ReachesThreshold_BlocksOnTheExactCountingViolation()
    {
        var blockCalls = new List<string>();
        var guard = new IpFloodGuard(40, 3, RecordingBlockDelegate(blockCalls), new SessionRegistry());

        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);
        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);
        Assert.Empty(blockCalls);

        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);

        Assert.Equal([Ip], blockCalls);
    }

    [Fact]
    public async Task RecordProtocolViolationAsync_DifferentIps_TrackedIndependently()
    {
        var blockCalls = new List<string>();
        var guard = new IpFloodGuard(40, 1, RecordingBlockDelegate(blockCalls), new SessionRegistry());

        await guard.RecordProtocolViolationAsync("203.0.113.1", CancellationToken.None);
        Assert.Equal(["203.0.113.1"], blockCalls);

        await guard.RecordProtocolViolationAsync("203.0.113.2", CancellationToken.None);
        Assert.Equal(["203.0.113.1", "203.0.113.2"], blockCalls);
    }

    [Fact]
    public async Task RecordProtocolViolationAsync_HourRollover_ResetsOnlyThatIpsOwnCounterAsACleanWindow()
    {
        var blockCalls = new List<string>();
        var hourOne = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var currentTime = hourOne;
        var guard = new IpFloodGuard(40, 3, RecordingBlockDelegate(blockCalls), new SessionRegistry(),
            () => currentTime);

        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);
        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);
        Assert.Empty(blockCalls);

        currentTime = hourOne.AddHours(1);

        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);

        Assert.Empty(blockCalls);

        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);
        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);

        Assert.Equal([Ip], blockCalls);
    }

    private static Func<string, CancellationToken, ValueTask> RecordingBlockDelegate(List<string> calls)
    {
        return (ip, _) =>
        {
            calls.Add(ip);
            return ValueTask.CompletedTask;
        };
    }
}
