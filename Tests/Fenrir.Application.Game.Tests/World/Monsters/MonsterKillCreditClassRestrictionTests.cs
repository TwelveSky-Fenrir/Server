using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Behavior contract <c>A3-kill-credit-class</c>: <c>Zone.SelectMonsterKillCredit</c>'s generic
///     max-damage path is scoped to <see cref="MonsterSpecialSort.Standard" /> (class 1, "standard monster")
///     only -- every other reachable class (2, 3, 4, 5, 6, 10) yields no recipient through that path, entirely
///     independent of whatever the monster's own attacker damage-tracking table holds. The five hardcoded
///     killing-blow-override catalog ids (746/777/1407/1408/1404) remain an unconditional bypass of BOTH the
///     max-damage scan and this class restriction, but only when a resolvable killing-blow attacker exists.
///     <see cref="MonsterEntity.RegisterAttackDamage" />/<see cref="MonsterEntity.SnapshotAttackDamage" /> are
///     <c>internal</c> with no <c>InternalsVisibleTo</c> grant to this test project, so every assertion here
///     observes the restriction only through <see cref="Zone" />'s public surface
///     (<see cref="Zone.TryDamageMonster" />, <see cref="Zone.TryDequeueDeadMonster" />,
///     <see cref="Zone.SpawnMonster" />) -- same posture as <c>MonsterAttackDamageTableTests</c>.
/// </summary>
public class MonsterKillCreditClassRestrictionTests
{
    /// <summary>
    ///     A zone with no <see cref="Monsters.MonsterAiSystem" />/spawn scheduler wired in -- these tests drive
    ///     <see cref="Zone.TryDamageMonster" /> directly against a manually-<see cref="Zone.SpawnMonster" />-ed
    ///     entity whose <see cref="MonsterEntity.SpecialSort" /> is pinned via <c>MonsterEntity.Create</c>'s own
    ///     test-only override parameter, bypassing the (still-open-question) live
    ///     <c>(Type, SpecialType) -&gt; sort</c> discriminator table entirely.
    /// </summary>
    private static Zone CreateZoneWithManualMonster(int serverIndex, int life, byte specialSort, int monsterId,
        out MonsterEntity monster)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var template = WorldDataTestRows.Monster(monsterId) with { Life = life };
        monster = MonsterEntity.Create(serverIndex, (uint)serverIndex, template, 1,
            0, 0, 0, 50, specialSort: specialSort);
        zone.SpawnMonster(monster);
        return zone;
    }

    /// <summary>Enters one character, draining the Enter command so it's live in the zone's own player map.</summary>
    private static void EnterCharacter(Zone zone, int characterId, string name)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, 1, name)));
    }

    [Theory]
    [InlineData(MonsterSpecialSort.TribeSymbolStone)]
    [InlineData(MonsterSpecialSort.Inert)]
    [InlineData(MonsterSpecialSort.AllianceStone)]
    [InlineData(MonsterSpecialSort.TribeGuard)]
    [InlineData(MonsterSpecialSort.Tower)]
    public void NonStandardClass_WithTrackedRealDamage_YieldsNoKillCredit(byte specialSort)
    {
        // Legacy's own death-time recipient-decision switch (S07_MyGame02.cpp:2794-2799) has exactly one
        // matching case -- class 1 -- and no default, so classes 2/3/4/5/10 (every non-Standard, non-CarThrower
        // reachable value) never resolve a recipient through the generic path, even though a real attacker
        // dealt the entire killing blow's damage directly.
        var zone = CreateZoneWithManualMonster(1, 100, specialSort, 600, out var monster);
        EnterCharacter(zone, 10, "A");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, monster.Life, 10, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Null(deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void CarThrowerClass_TracksRealDamage_ButStillYieldsNoKillCredit_AsymmetryEdgeCase()
    {
        // The one genuinely surprising legacy asymmetry the source contract calls out: class 6 ("car-thrower")
        // attackers ARE eligible to be registered into the same shared attacker table class-1 monsters use
        // (S07_MyGame02.cpp:2163-2169,2459-2468) -- Fenrir's TryDamageMonster registers every class
        // unconditionally, a harmless superset -- but the recipient-decision switch still has no case for
        // class 6, so none of that tracked data is ever read back. A single attacker dealing 100% of the
        // killing blow's damage still walks away with no credit.
        var zone = CreateZoneWithManualMonster(1, 250, MonsterSpecialSort.CarThrower, 600, out var monster);
        EnterCharacter(zone, 10, "A");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 100, 10, out var died1, out _));
        Assert.False(died1);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, monster.Life, 10, out var died2, out _));
        Assert.True(died2);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Null(deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void StandardClass_CreditsHighestDamageAttacker_ControlCase()
    {
        // Regression guard for the class gate itself: an explicitly-pinned Standard (class 1) monster must
        // still resolve kill credit through the ordinary max-damage path exactly as before -- the new class
        // check must not accidentally suppress the one class it is supposed to let through.
        var zone = CreateZoneWithManualMonster(1, 100, MonsterSpecialSort.Standard, 600, out var monster);
        EnterCharacter(zone, 10, "A");
        EnterCharacter(zone, 11, "B");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 30, 10, out var died1, out _));
        Assert.False(died1);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 70, 11, out var died2, out _));
        Assert.True(died2);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(11, deadMonster!.KillerCharacterId); // B's 70 > A's 30
    }

    [Fact]
    public void OverrideCatalogId_ForcesCreditToKillingBlowAttacker_RegardlessOfNonStandardClass()
    {
        // The five hardcoded killing-blow-override catalog ids (746/777/1407/1408/1404) bypass the max-damage
        // computation AND the class restriction entirely -- an otherwise fully-ineligible class (Inert, 3)
        // still credits the killing blow's own attacker.
        var zone = CreateZoneWithManualMonster(1, 100, MonsterSpecialSort.Inert, 746, out var monster);
        EnterCharacter(zone, 10, "A");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, monster.Life, 10, out var died, out _));
        Assert.True(died);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(10, deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void OverrideCatalogId_WithoutResolvableKillingBlowAttacker_FallsBackToClassRestriction()
    {
        // The override only ever applies when TryDamageMonster's own killing-blow attacker argument is
        // resolvable (non-null) -- a death that bypasses attacker attribution entirely (environment/DoT,
        // modeled here as a null-attacker finishing blow) still falls through to the class restriction below
        // it, even on one of the five override catalog ids.
        var zone = CreateZoneWithManualMonster(1, 100, MonsterSpecialSort.Inert, 746, out var monster);
        EnterCharacter(zone, 10, "A");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 40, 10, out var died1, out _));
        Assert.False(died1);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, monster.Life, null, out var died2, out _));
        Assert.True(died2);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Null(deadMonster!.KillerCharacterId); // Inert (3) is not Standard, and no attacker to override with
    }
}
