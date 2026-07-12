using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterKillCreditClassRestrictionTests
{
    private static Zone CreateZoneWithManualMonster(int serverIndex, int life, byte specialSort, int monsterId,
        out MonsterEntity monster)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var template = WorldDataTestRows.Monster(monsterId) with { Life = life };
        monster = MonsterEntity.Create(serverIndex, (uint)serverIndex, template, 1,
            0, 0, 0, specialSort: specialSort);
        zone.SpawnMonster(monster);
        return zone;
    }

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
        var zone = CreateZoneWithManualMonster(1, 100, MonsterSpecialSort.Standard, 600, out var monster);
        EnterCharacter(zone, 10, "A");
        EnterCharacter(zone, 11, "B");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 30, 10, out var died1, out _));
        Assert.False(died1);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 70, 11, out var died2, out _));
        Assert.True(died2);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Equal(11, deadMonster!.KillerCharacterId);
    }

    [Fact]
    public void OverrideCatalogId_ForcesCreditToKillingBlowAttacker_RegardlessOfNonStandardClass()
    {
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
        var zone = CreateZoneWithManualMonster(1, 100, MonsterSpecialSort.Inert, 746, out var monster);
        EnterCharacter(zone, 10, "A");
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryDamageMonster(monster.ServerIndex, 40, 10, out var died1, out _));
        Assert.False(died1);
        Assert.True(zone.TryDamageMonster(monster.ServerIndex, monster.Life, null, out var died2, out _));
        Assert.True(died2);

        Assert.True(zone.TryDequeueDeadMonster(out var deadMonster));
        Assert.Null(deadMonster!.KillerCharacterId);
    }
}
