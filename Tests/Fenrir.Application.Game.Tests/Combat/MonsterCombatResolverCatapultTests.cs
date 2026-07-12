using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

public class MonsterCombatResolverCatapultTests
{
    private const int MonsterAttackPower = 20000;
    private const int DefenderDefensePower = 5000;

    private static MonsterEntity Monster(byte? specialSort)
    {
        var template = WorldDataTestRows.Monster(700) with
        {
            Life = 1_000_000,
            AttackPower = MonsterAttackPower,
            AttackSuccess = 100,
            Critical = 0,
            ElementAttackPower = 0,
            ElementDefensePower = 0,
            RadiusInfo1 = 100,
            RadiusInfo2 = 100
        };

        return MonsterEntity.Create(1, 1u, template, 1, 0f, 0f, 0f, random: new ScriptedRandomSource(0),
            specialSort: specialSort);
    }

    private static CombatantSnapshot Defender()
    {
        var stats = new EffectiveStats(1_000_000, 1_000_000, 0, DefenderDefensePower, 0, 0, 0, 0, 0, 0, 0);
        return new CombatantSnapshot(2, 1, false, 1_000_000, 1_000_000, 0f, 0f, 0f, null, stats, 0);
    }

    private static ScriptedRandomSource NoVarianceNoCritRng()
    {
        return new ScriptedRandomSource(0, 0, 50);
    }

    [Fact]
    public void CarThrowerMonster_AddsFlat15000AttackPowerBeforeDefenseSubtraction()
    {
        var outcome = MonsterCombatResolver.ResolveMvpAttack(Monster(MonsterSpecialSort.CarThrower), Defender(),
            TimeSpan.Zero, NoVarianceNoCritRng());

        Assert.True(outcome.Hit);
        Assert.Equal(30000, outcome.DamageApplied);
    }

    [Fact]
    public void StandardMonster_DoesNotAddCatapultBonus()
    {
        var outcome = MonsterCombatResolver.ResolveMvpAttack(Monster(MonsterSpecialSort.Standard), Defender(),
            TimeSpan.Zero, NoVarianceNoCritRng());

        Assert.True(outcome.Hit);
        Assert.Equal(15000, outcome.DamageApplied);
        Assert.Equal(MonsterCombatResolver.CatapultAttackPowerBonus, 30000 - outcome.DamageApplied);
    }
}
