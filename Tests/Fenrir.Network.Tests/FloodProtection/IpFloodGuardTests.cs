using System.Net;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Tests.Sessions;

namespace Fenrir.Network.Tests.FloodProtection;

// Unit coverage of IpFloodGuard in isolation -- SessionLoopTests covers Trigger B's actual production wiring
// through SessionLoop's ProtocolViolationException handling; LoginConnectionHost/GameConnectionHost wire
// Trigger A, out of this test project's scope (Hosting layer), so its accept-time ordering isn't re-tested
// here beyond the guard's own return-value contract.
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

    // Trigger A boundary (contract): strict greater-than, so the block trips on the (threshold+1)th
    // concurrent connection, not the threshold'th itself.
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

    // Deliberate fix over legacy's own bare self-referential Quit() bug (contract Side effects §2): every
    // session sharing the blocked IP is kicked, not just the newly-arriving one, and sessions on a different
    // IP are left alone.
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

    // Contract's Error/failure semantics: persisting the block must never fault the accept path.
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

    // Trigger B boundary (contract): greater-than-or-equal, so the block trips exactly on the threshold'th
    // tallied violation, unlike Trigger A's strict greater-than.
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

        // A second, different IP's very first violation must independently trip its own threshold too -- not
        // be silently absorbed the way legacy's shared/global "last hour" variable would (contract Edge Cases).
        await guard.RecordProtocolViolationAsync("203.0.113.2", CancellationToken.None);
        Assert.Equal(["203.0.113.1", "203.0.113.2"], blockCalls);
    }

    // Deliberately NOT reproducing legacy's two known bugs in the dead MyUserPacketError function (contract
    // Edge Cases): a single process-wide "last hour" sentinel that (a) free-passes the first violation after
    // every hour boundary and (b) resets every OTHER ip's tally too. This is a clean per-IP tumbling window.
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
        Assert.Empty(blockCalls); // 2 of 3 in hour one -- not yet tripped

        currentTime = hourOne.AddHours(1); // roll over to a new wall-clock hour

        await guard.RecordProtocolViolationAsync(Ip, CancellationToken.None);

        // The 3rd lifetime violation is only the 1st of the new hour -- must NOT trip a threshold of 3.
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
