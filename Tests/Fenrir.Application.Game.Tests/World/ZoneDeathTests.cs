using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone.ApplyDeath" /> and its automatic revive: revive is always in place -- the legacy only
///     auto-clears the death flag locally after the delay, and cross-zone "return to town" is a separate,
///     client-driven transfer (<c>ZoneMoveHandler</c>), not this timer.
/// </summary>
public class ZoneDeathTests
{
    [Fact]
    public void ApplyDeath_SetsLifeZeroAndIsDead()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(0, state!.Life);
        Assert.True(state.IsDead);
    }

    [Fact]
    public void ApplyDeath_UnknownCharacter_IsIgnored()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.ApplyDeath(999); // must not throw

        Assert.False(zone.TryGetPlayer(999, out _));
    }

    [Fact]
    public void ApplyDeath_AlreadyDead_DoesNotRearmTheReviveTimer()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);
        zone.Tick(TimeSpan.FromSeconds(3));

        zone.ApplyDeath(10); // duplicate killing blow -- must not push the revive further out

        zone.Tick(TimeSpan.FromSeconds(3)); // total elapsed since the FIRST ApplyDeath: 6s > the 5s delay

        Assert.True(zone.TryGetPlayer(10, out var revived));
        Assert.False(revived!.IsDead);
    }

    [Fact]
    public void Revive_AfterTheDelay_ClearsDeathInPlace_SamePositionSameZone()
    {
        var zone = ZoneTestKit.CreateZone(2);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2, posX: 500f, posZ: 500f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);

        zone.Tick(TimeSpan.FromSeconds(4));
        Assert.True(zone.TryGetPlayer(10, out var stillDead));
        Assert.True(stillDead!.IsDead);
        Assert.Equal(500f, stillDead.PosX);
        Assert.Equal(500f, stillDead.PosZ);

        // Past the 5s delay: revives in place -- NOT teleported anywhere, regardless of tribe/zone.
        zone.Tick(TimeSpan.FromSeconds(2));

        Assert.True(zone.TryGetPlayer(10, out var revived));
        Assert.False(revived!.IsDead);
        Assert.Equal(1, revived.Life);
        Assert.Equal(2, revived.MapId);
        Assert.Equal(500f, revived.PosX);
        Assert.Equal(500f, revived.PosZ);
    }

    [Fact]
    public void Revive_NeverInitiatesACrossZoneHandoff()
    {
        // a bare Zone (no ZoneRegistry backref) proves no cross-zone resolution is even attempted
        var zone = ZoneTestKit.CreateZone(2);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);
        zone.Tick(TimeSpan.FromSeconds(6));

        Assert.True(zone.TryGetPlayer(10, out var revived));
        Assert.False(revived!.IsDead);
        Assert.Equal(1, revived.Life);
        Assert.Equal(2, revived.MapId);
    }

    [Fact]
    public void DeadPlayer_HandedOffToAnotherZone_ArrivesStillDead_NotSilentlyRevivedOrLost()
    {
        var source = ZoneTestKit.CreateZone(2);
        var target = ZoneTestKit.CreateZone(3);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        source.ApplyDeath(10);
        source.Post(ZoneCommand.Leave(10, target)); // e.g. an explicit CZ_DEMAND_ZONE_SERVER_INFO_2 while dead
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(source.TryGetPlayer(10, out _));

        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var arrived));
        Assert.Equal(3, arrived!.MapId);
        Assert.True(arrived.IsDead);
    }
}
