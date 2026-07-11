using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Tests.Simulation;

public class DeathGateTickSystemTests
{
    private static (Zone Zone, WorldStateService WorldState) SetUp(short mapId, WorldStateService? worldState = null)
    {
        var state = worldState ?? ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(mapId, simulationSystems: [new DeathGateTickSystem(state)],
            worldState: state);
        return (zone, state);
    }

    private static void EnterAndKill(Zone zone, int characterId, byte tribe, DeathCause cause = DeathCause.MonsterKill)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.ApplyDeath(characterId, cause);
    }

    [Fact]
    public void UnconditionalZone_BeforeTenTicks_StaysDeadAndFlagged()
    {
        var (zone, _) = SetUp(999);
        EnterAndKill(zone, 10, 1);

        zone.Tick(TimeSpan.FromMilliseconds(4500));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.True(player!.IsDead);
        Assert.True(player.ReviveHackFlag);
    }

    [Fact]
    public void UnconditionalZone_AtTenTicks_GrantsReviveEligibility_ClearsEveryDeathGateFlag()
    {
        var (zone, _) = SetUp(999);
        EnterAndKill(zone, 10, 1);

        zone.Tick(TimeSpan.FromMilliseconds(5000));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.IsDead);
        Assert.Equal(1, player.Life);
        Assert.False(player.ReviveHackFlag);
        Assert.True(player.CanUseConsumables);
        Assert.Equal(0, player.TicksSinceDeath);
    }

    [Fact]
    public void FactionTerritory_AvatarTribeMatchesOwner_GrantsEligibilityAtTenTicks()
    {
        var (zone, _) = SetUp(2);
        EnterAndKill(zone, 10, 0);

        zone.Tick(TimeSpan.FromMilliseconds(5000));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.IsDead);
    }

    [Fact]
    public void FactionTerritory_MismatchedTribeNoAlliance_StaysDead_PastTheTenTickMark()
    {
        var (zone, _) = SetUp(2);
        EnterAndKill(zone, 10, 1);

        zone.Tick(TimeSpan.FromMilliseconds(5000));
        zone.Tick(TimeSpan.FromMilliseconds(5000));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.True(player!.IsDead);
    }

    [Fact]
    public void FactionTerritory_AllianceFormsAfterTheTenTickMark_GrantsEligibilityOnALaterTick()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var (zone, _) = SetUp(2, worldState);
        EnterAndKill(zone, 10, 1);

        zone.Tick(TimeSpan.FromMilliseconds(5000));
        Assert.True(zone.TryGetPlayer(10, out var stillDead));
        Assert.True(stillDead!.IsDead);

        worldState.SetAllianceOffer(1, 0, true);
        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.True(zone.TryGetPlayer(10, out var revived));
        Assert.False(revived!.IsDead);
    }

    [Fact]
    public void AlwaysBlockedZone_NeverGrantsEligibility_EvenFarPastTheTenTickMark()
    {
        var (zone, _) = SetUp(200);
        EnterAndKill(zone, 10, 0);

        zone.Tick(TimeSpan.FromSeconds(4));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.True(player!.IsDead);
    }

    [Fact]
    public void AntiAbuseForceQuit_FiresAtFiftyTicks_WhenStillFlaggedAndNotEligible()
    {
        var (zone, _) = SetUp(2);
        var (session, _) = ZoneTestKit.CreateSession(10);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, zone.MapId, tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.ApplyDeath(10, DeathCause.MonsterKill);

        zone.Tick(TimeSpan.FromMilliseconds(24_500));
        Assert.Null(session.DisconnectReason);

        zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public void AntiAbuseForceQuit_DoesNotFire_WhenEligibilityIsGrantedTheSameTick()
    {
        var (zone, _) = SetUp(999);
        var (session, _) = ZoneTestKit.CreateSession(10);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, zone.MapId, tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.ApplyDeath(10, DeathCause.MonsterKill);

        zone.Tick(TimeSpan.FromSeconds(30));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.IsDead);
    }

    [Fact]
    public void DuelDeath_NeverArmsTheAntiAbuseFlag_AndIsNeverForceQuit()
    {
        var (zone, _) = SetUp(2);
        var (session, _) = ZoneTestKit.CreateSession(10);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, zone.MapId, tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.ApplyDeath(10, DeathCause.Duel);

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.ReviveHackFlag);

        zone.Tick(TimeSpan.FromSeconds(30));

        Assert.Null(session.DisconnectReason);
        Assert.True(player.IsDead);
    }
}
