using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="DeathGateTickSystem" />: the territorial revive-eligibility recheck (side effect 1)
///     and the <c>mProtect_ReviveHack</c> 50-tick anti-abuse force-quit safety valve (side effect 2).
///     Broadcast suppression (side effect 3) is covered in <c>ZoneDeathTests</c> alongside the rest of
///     <see cref="Zone.ApplyDeath" />/<see cref="Zone.GrantReviveEligibility" />.
/// </summary>
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
        var (zone, _) = SetUp(999); // 999 is outside every faction-territory block and 200/322/323
        EnterAndKill(zone, 10, tribe: 1);

        // 9 legacy ticks (4.5 s) -- one short of the 10-tick eligibility threshold.
        zone.Tick(TimeSpan.FromMilliseconds(4500));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.True(player!.IsDead);
        Assert.True(player.ReviveHackFlag);
    }

    [Fact]
    public void UnconditionalZone_AtTenTicks_GrantsReviveEligibility_ClearsEveryDeathGateFlag()
    {
        var (zone, _) = SetUp(999);
        EnterAndKill(zone, 10, tribe: 1);

        zone.Tick(TimeSpan.FromMilliseconds(5000)); // exactly 10 legacy ticks

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
        var (zone, _) = SetUp(2); // faction-0 territory block
        EnterAndKill(zone, 10, tribe: 0); // matches the owning faction

        zone.Tick(TimeSpan.FromMilliseconds(5000));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.IsDead);
    }

    [Fact]
    public void FactionTerritory_MismatchedTribeNoAlliance_StaysDead_PastTheTenTickMark()
    {
        var (zone, _) = SetUp(2); // faction-0 territory block
        EnterAndKill(zone, 10, tribe: 1); // does not match, no alliance configured

        zone.Tick(TimeSpan.FromMilliseconds(5000)); // 10 ticks
        zone.Tick(TimeSpan.FromMilliseconds(5000)); // 20 ticks -- still re-checked every tick, still fails

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.True(player!.IsDead);
    }

    [Fact]
    public void FactionTerritory_AllianceFormsAfterTheTenTickMark_GrantsEligibilityOnALaterTick()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var (zone, _) = SetUp(2, worldState); // faction-0 territory block
        EnterAndKill(zone, 10, tribe: 1);

        zone.Tick(TimeSpan.FromMilliseconds(5000)); // 10 ticks: not yet allied -- still dead
        Assert.True(zone.TryGetPlayer(10, out var stillDead));
        Assert.True(stillDead!.IsDead);

        // Tribe 1 becomes allied with tribe 0 (the block's owner) between ticks.
        worldState.SetAllianceOffer(1, 0, true);
        zone.Tick(TimeSpan.FromMilliseconds(500)); // one more legacy tick -- recheck runs again, now eligible

        Assert.True(zone.TryGetPlayer(10, out var revived));
        Assert.False(revived!.IsDead);
    }

    [Fact]
    public void AlwaysBlockedZone_NeverGrantsEligibility_EvenFarPastTheTenTickMark()
    {
        var (zone, _) = SetUp(200);
        EnterAndKill(zone, 10, tribe: 0);

        zone.Tick(TimeSpan.FromSeconds(4)); // 8 legacy ticks -- under the 50-tick force-quit mark, well past 10

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.True(player!.IsDead);
    }

    [Fact]
    public void AntiAbuseForceQuit_FiresAtFiftyTicks_WhenStillFlaggedAndNotEligible()
    {
        var (zone, _) = SetUp(2); // faction-0 territory: mismatched tribe below never resolves
        var (session, _) = ZoneTestKit.CreateSession(10);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, zone.MapId, tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.ApplyDeath(10, DeathCause.MonsterKill); // armed cause

        zone.Tick(TimeSpan.FromMilliseconds(24_500)); // 49 legacy ticks -- one short of the force-quit mark
        Assert.Null(session.DisconnectReason);

        zone.Tick(TimeSpan.FromMilliseconds(500)); // 50th legacy tick

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public void AntiAbuseForceQuit_DoesNotFire_WhenEligibilityIsGrantedTheSameTick()
    {
        // A single large catch-up tick (well past both the 10- and 50-tick marks at once) in an unconditional
        // zone grants eligibility first; the force-quit check must see the already-cleared flag and not fire.
        var (zone, _) = SetUp(999);
        var (session, _) = ZoneTestKit.CreateSession(10);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, zone.MapId, tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.ApplyDeath(10, DeathCause.MonsterKill);

        zone.Tick(TimeSpan.FromSeconds(30)); // 60 legacy ticks in one jump -- past the 50-tick mark

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.IsDead);
    }

    [Fact]
    public void DuelDeath_NeverArmsTheAntiAbuseFlag_AndIsNeverForceQuit()
    {
        // Faction-0 territory, mismatched tribe -- would never resolve on its own, but a duel death never
        // arms ReviveHackFlag, so the 50-tick force-quit must never fire for it.
        var (zone, _) = SetUp(2);
        var (session, _) = ZoneTestKit.CreateSession(10);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, zone.MapId, tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.ApplyDeath(10, DeathCause.Duel);

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.ReviveHackFlag);

        zone.Tick(TimeSpan.FromSeconds(30)); // well past the 50-tick mark

        Assert.Null(session.DisconnectReason);
        Assert.True(player.IsDead); // still stuck (faction mismatch, no alliance) -- just never kicked for it
    }
}
