using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TempRegistrationIdleSweepTests
{
    [Fact]
    public void Sweep_LeavesFreshRegistrationsConnected()
    {
        var registry = new TribeQuotaRegistry();
        var sweep = new TempRegistrationIdleSweep(registry, NullLogger<TempRegistrationIdleSweep>.Instance);
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(10, 100);
        registry.Record(session, 0, 10, 100, DateTimeOffset.UtcNow);

        sweep.Sweep(DateTimeOffset.UtcNow);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Sweep_DisconnectsAndReleases_ANotYetReadyRegistrationPastTheTimeout()
    {
        var registry = new TribeQuotaRegistry();
        var sweep = new TempRegistrationIdleSweep(registry, NullLogger<TempRegistrationIdleSweep>.Instance);
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(10, 100);

        var registeredAt = DateTimeOffset.UtcNow - TempRegistrationIdleSweep.IdleTimeout - TimeSpan.FromSeconds(1);
        registry.Record(session, 1, 10, 100, registeredAt);

        sweep.Sweep(DateTimeOffset.UtcNow);

        Assert.Equal(DisconnectReason.IdleTimeout, session.DisconnectReason);
        Assert.Equal(0, registry.Count);
        Assert.Equal(0, registry.CountForTribe(1));
    }

    [Fact]
    public void Sweep_DoesNotDisconnectAConnectionThatAlreadyReachedInWorld()
    {
        var registry = new TribeQuotaRegistry();
        var sweep = new TempRegistrationIdleSweep(registry, NullLogger<TempRegistrationIdleSweep>.Instance);
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(10, 100);
        session.MarkRegistering();
        session.MarkInWorld();

        var registeredAt = DateTimeOffset.UtcNow - TempRegistrationIdleSweep.IdleTimeout - TimeSpan.FromMinutes(5);
        registry.Record(session, 1, 10, 100, registeredAt);

        sweep.Sweep(DateTimeOffset.UtcNow);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(1, registry.Count);
    }
}
