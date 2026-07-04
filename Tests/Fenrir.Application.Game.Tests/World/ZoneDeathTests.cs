using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone.ApplyDeath" /> and its automatic revive sweep (M1 plan, Phase C/V1 item 3): the
///     immediate death effects (Life=0, IsDead, revive scheduled) and the ~5 s ("10 legacy ticks",
///     <see cref="Simulation.SimulationClock.DeathReviveDelay" />) automatic revive itself. The revive is ALWAYS in
///     place (report 12 §4.2/§4.3: the legacy only auto-clears the death flag locally after the delay -- an
///     actual cross-zone "return to town" transfer is client-driven, via CZ_DEMAND_ZONE_SERVER_INFO_2 Sort=3,
///     already covered by <c>ZoneMoveHandler</c>, not by this timer). A prior revision of
///     this suite tested a since-removed cross-zone auto-teleport-to-capital mechanism (review finding: it
///     both diverged from the documented legacy behavior and lost a pending revive's destination across a
///     zone handoff, since IsDead traveled but the destination fields did not) -- those tests are replaced
///     below by ones pinning the corrected, strictly-in-place behavior.
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

        // Not yet due.
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
        // Regression guard for the removed auto-teleport-to-capital mechanism: dying in a zone that is nobody's
        // capital must still revive the player IN THAT SAME ZONE, never hand them off anywhere -- a bare Zone
        // (no ZoneRegistry backref) proves no cross-zone resolution is even attempted.
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
        // The critical bug this pins against: PlayerEnterData/HandleEnter must carry IsDead (already did)
        // through an in-process handoff without losing/misinterpreting any death-related state on arrival --
        // there is no longer any Revive* destination data to lose, since revive is always in place now.
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
        // IsDead survived the handoff intact (it may since revive shortly after on the target zone's own
        // schedule, but it must never arrive silently "alive" nor throw/misbehave on arrival).
        Assert.True(arrived.IsDead);
    }
}
