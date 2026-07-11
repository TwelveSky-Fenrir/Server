using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the B16 wiring gap this workstream closes: <c>Zone.ApplyPvpKillMountExperience</c>, called from
///     <c>Zone.ApplyPvpKillExperience</c> alongside the pre-existing pet-growth grant -- previously nothing ever
///     invoked <see cref="MountKillExperienceCalculator.ComputeGain" />, so
///     <see cref="PlayerRuntimeState.MountAccumulatedExp" /> stayed frozen at 0 for every character regardless of
///     kill count. Reuses <c>ZonePvpKillRewardsTests</c>' own <c>SetUpZone</c>/<c>KillDefender</c> shape.
/// </summary>
public class ZonePvpKillMountExperienceTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats WeakDefender =
        new(1000, 1000, 0, 200, 100, 0, 0, 0, 0, 0, 0);

    private static AttackForProtocol MeleeRequest(int attackerId, int defenderId)
    {
        return new AttackForProtocol
        {
            Case = 2,
            ServerIndex1 = attackerId,
            UniqueNumber1 = unchecked((uint)attackerId),
            ServerIndex2 = defenderId,
            UniqueNumber2 = unchecked((uint)defenderId),
            SenderLocation = [100, 0, 100],
            AttackActionValue1 = 1,
            AttackActionValue2 = 0,
            AttackActionValue3 = 0,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
    }

    private static Zone SetUpZone(short mapId = 1)
    {
        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(0, 0));

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: 0)));

        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: 1)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Stats = WeakDefender;
        defender.ActionSort = 1; // legal, already-acting pose -- see ZoneAttackTests' own TwoPlayerZone remarks
        attacker.AttackSubPacketCeiling = int.MaxValue; // see ZonePvpKillRewardsTests' own remarks

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past the zone-entry protect window
        return zone;
    }

    private static void KillDefender(Zone zone)
    {
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1; // one hit will kill regardless of exact damage roll

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
    }

    [Fact]
    public void ActivelyMountedAttacker_WithFedActivity_GainsTheConfiguredBaseAmount()
    {
        var zone = SetUpZone();
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.AnimalIndex = 10; // slot 0, actively mounted (10-19 offset band)
        attacker.MountActivity = attacker.MountActivity.SetItem(0, 50); // fed, > 0
        attacker.MountAccumulatedExp = attacker.MountAccumulatedExp.SetItem(0, 0);

        KillDefender(zone);

        Assert.Equal(MountKillExperienceCalculator.PlaceholderBaseExperiencePerKill, attacker.MountAccumulatedExp[0]);
    }

    [Fact]
    public void ActivelyMountedAttacker_WithUnfedActivity_GainsNothing()
    {
        var zone = SetUpZone();
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.AnimalIndex = 10; // actively mounted, but activity is still 0 (never fed)
        Assert.Equal(0, attacker.MountActivity[0]);

        KillDefender(zone);

        Assert.Equal(0, attacker.MountAccumulatedExp[0]);
    }

    [Fact]
    public void SelectedButNotActivelyMounted_GainsNothing()
    {
        var zone = SetUpZone();
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.AnimalIndex = 3; // garage slot SELECTED (0-9 band), not actively ridden
        attacker.MountActivity = attacker.MountActivity.SetItem(3, 50);

        KillDefender(zone);

        Assert.Equal(0, attacker.MountAccumulatedExp[3]);
    }

    [Fact]
    public void NoMountSelected_GainsNothing_AndDoesNotThrow()
    {
        var zone = SetUpZone();
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(-1, attacker!.AnimalIndex); // default: no mount selected at all

        KillDefender(zone); // must not throw (regression guard against indexing with AnimalIndex == -1)

        Assert.All(attacker.MountAccumulatedExp, exp => Assert.Equal(0, exp));
    }

    [Fact]
    public void DoubleExpAndSessionExpUpFlag_BothDoubleTheGain()
    {
        var zone = SetUpZone();
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.AnimalIndex = 10;
        attacker.MountActivity = attacker.MountActivity.SetItem(0, 50);
        attacker.AnimalDoubleExp = 5; // running
        attacker.MountExpUp = true;

        KillDefender(zone);

        Assert.Equal(MountKillExperienceCalculator.PlaceholderBaseExperiencePerKill * 4, attacker.MountAccumulatedExp[0]);
    }

    [Fact]
    public void GainIsClampedAtMaxMountExperience()
    {
        var zone = SetUpZone();
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.AnimalIndex = 10;
        attacker.MountActivity = attacker.MountActivity.SetItem(0, 50);
        attacker.MountAccumulatedExp = attacker.MountAccumulatedExp.SetItem(0, MountActivityExpCodec.MaxExp - 1);

        KillDefender(zone);

        Assert.Equal(MountActivityExpCodec.MaxExp, attacker.MountAccumulatedExp[0]);
    }

    [Fact]
    public void AlreadyAtMaxMountExperience_GainsNoFurther_AndComputeGainItselfReturnsZero()
    {
        // MountKillExperienceCalculator.ComputeGain's own >= MaxExp gate means the grant is a no-op once a slot
        // is already fully capped -- distinct from GainIsClampedAtMaxMountExperience's "would overflow past the
        // cap" case, which still grants a (clamped) nonzero amount.
        var zone = SetUpZone();
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.AnimalIndex = 10;
        attacker.MountActivity = attacker.MountActivity.SetItem(0, 50);
        attacker.MountAccumulatedExp = attacker.MountAccumulatedExp.SetItem(0, MountActivityExpCodec.MaxExp);

        KillDefender(zone);

        Assert.Equal(MountActivityExpCodec.MaxExp, attacker.MountAccumulatedExp[0]);
    }
}
