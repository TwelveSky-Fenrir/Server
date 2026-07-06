using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeQuotaRegistryTests
{
    [Fact]
    public void CountForTribe_IsAlwaysRecomputedFromCurrentlyRecordedEntries_NotCached()
    {
        var registry = new TribeQuotaRegistry();
        var (sessionA, _) = ZoneTestKit.CreateSession(1);
        var (sessionB, _) = ZoneTestKit.CreateSession(2);
        var now = DateTimeOffset.UtcNow;

        registry.Record(sessionA, tribe: 1, accountId: 10, characterId: 100, now);
        registry.Record(sessionB, tribe: 1, accountId: 11, characterId: 101, now);

        Assert.Equal(2, registry.CountForTribe(1));
        Assert.Equal(0, registry.CountForTribe(2));

        registry.Release(sessionA.SessionId);

        // Recomputed, not decremented from a cached counter: releasing one of the two now shows exactly one.
        Assert.Equal(1, registry.CountForTribe(1));
    }

    [Fact]
    public void Record_UsesTheSuppliedTribe_NotWhateverTheConnectionMightDeclareLater()
    {
        var registry = new TribeQuotaRegistry();
        var (session, _) = ZoneTestKit.CreateSession(1);

        // Simulates the "declared tribe used only for the threshold check, upstream-resolved tribe recorded"
        // edge case: caller passes the resolved tribe (2), not whatever was declared on the wire.
        registry.Record(session, tribe: 2, accountId: 10, characterId: 100, DateTimeOffset.UtcNow);

        Assert.Equal(1, registry.CountForTribe(2));
        Assert.Equal(0, registry.CountForTribe(0));
    }

    [Fact]
    public void Release_UnknownSession_IsANoOp()
    {
        var registry = new TribeQuotaRegistry();

        Assert.False(registry.Release(sessionId: 999));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Release_RemovesTheEntry_AndIsIdempotent()
    {
        var registry = new TribeQuotaRegistry();
        var (session, _) = ZoneTestKit.CreateSession(1);
        registry.Record(session, tribe: 0, accountId: 10, characterId: 100, DateTimeOffset.UtcNow);

        Assert.True(registry.Release(session.SessionId));
        Assert.Equal(0, registry.Count);
        Assert.False(registry.Release(session.SessionId)); // second release is a harmless no-op
    }

    [Fact]
    public void SnapshotIdle_ExcludesConnectionsThatAlreadyReachedInWorld()
    {
        var registry = new TribeQuotaRegistry();
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(10, 100);
        session.MarkRegistering();
        session.MarkInWorld();

        var registeredAt = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10);
        registry.Record(session, tribe: 0, accountId: 10, characterId: 100, registeredAt);

        var idle = registry.SnapshotIdle(TempRegistrationIdleSweep.IdleTimeout, DateTimeOffset.UtcNow);

        Assert.Empty(idle);
    }

    [Fact]
    public void SnapshotIdle_ExcludesConnectionsYoungerThanTheTimeout()
    {
        var registry = new TribeQuotaRegistry();
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(10, 100);

        var registeredAt = DateTimeOffset.UtcNow - (TempRegistrationIdleSweep.IdleTimeout - TimeSpan.FromSeconds(1));
        registry.Record(session, tribe: 0, accountId: 10, characterId: 100, registeredAt);

        var idle = registry.SnapshotIdle(TempRegistrationIdleSweep.IdleTimeout, DateTimeOffset.UtcNow);

        Assert.Empty(idle);
    }

    [Fact]
    public void SnapshotIdle_IncludesNotYetReadyConnectionsAtOrPastTheTimeout()
    {
        var registry = new TribeQuotaRegistry();
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(10, 100); // TicketConsumed -- never reached Registering/InWorld

        var registeredAt = DateTimeOffset.UtcNow - TempRegistrationIdleSweep.IdleTimeout;
        registry.Record(session, tribe: 2, accountId: 10, characterId: 100, registeredAt);

        var idle = registry.SnapshotIdle(TempRegistrationIdleSweep.IdleTimeout, DateTimeOffset.UtcNow);

        var entry = Assert.Single(idle);
        Assert.Equal(session.SessionId, entry.Session.SessionId);
        Assert.Equal(2, entry.Tribe);
        Assert.Equal(ZoneSessionState.TicketConsumed, entry.Session.State);
    }
}
