using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Combat;

public class MonsterCombatResolverTribeSymbolDamageUpBonusTests
{
    private const int AttackerAttackPower = 1000;

    private static MonsterEntity Monster()
    {
        var template = WorldDataTestRows.Monster(700) with
        {
            Life = 1_000_000,
            DefensePower = 0,
            AttackBlock = 0,
            ElementDefensePower = 0
        };

        return MonsterEntity.Create(1, 1u, template, 1, 0f, 0f, 0f, 100f);
    }

    private static CombatantSnapshot Attacker(short level = 1)
    {
        var stats = new EffectiveStats(1000, 0, AttackerAttackPower, 0, 100, 0, 0, 0, 0, 0, 0);
        return new CombatantSnapshot(10, 0, false, 1000, 1000, 0f, 0f, 0f, null, stats, 0, level);
    }

    private static AttackForProtocol MeleeRequest(MonsterEntity monster)
    {
        return new AttackForProtocol
        {
            Case = 3,
            ServerIndex1 = 10,
            UniqueNumber1 = 10u,
            ServerIndex2 = monster.ServerIndex,
            UniqueNumber2 = monster.UniqueNumber,
            SenderLocation = [0, 0, 0],
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

    private static ScriptedRandomSource NoVarianceRng()
    {
        return new ScriptedRandomSource(0);
    }

    [Theory]
    [InlineData(1, 1_500)]
    [InlineData(2, 2_000)]
    [InlineData(3, 2_500)]
    [InlineData(TribeSymbolCombatModifiers.MaxDamageUpBonusIncrementCount, 3_000)]
    public void IncrementCountAboveZero_AddsFlatFiveHundredPerIncrement(int incrementCount, int expectedDamage)
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), false, 0,
            0, 0f,
            incrementCount);

        Assert.True(outcome.Hit);
        Assert.Equal(expectedDamage, outcome.DamageApplied);
        Assert.Equal(expectedDamage, outcome.ViewDamage);
    }

    [Fact]
    public void IncrementCountZero_NoAdditionAtAll()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), false, 0,
            0, 0f,
            0);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }

    [Fact]
    public void DefaultParameter_OmittedEntirely_BehavesAsNoBonus_ExistingCallersUnaffected()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), false, 0, 0);

        Assert.True(outcome.Hit);
        Assert.Equal(AttackerAttackPower, outcome.DamageApplied);
    }

    [Fact]
    public void BonusCompoundsBeforeTheDamageDownMalus_MalusAppliesToTheBonusedTotal()
    {
        var monster = Monster();
        var outcome = MonsterCombatResolver.ResolvePvmAttack(Attacker(113), monster, MeleeRequest(monster),
            TimeSpan.Zero, NoVarianceRng(), false, 0,
            0,
            TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty,
            1);

        Assert.True(outcome.Hit);
        Assert.Equal(1_200, outcome.DamageApplied);
    }

    [Fact]
    public void DamageUpBonusFlatPerIncrement_IsTheCitedFiveHundred()
    {
        Assert.Equal(500, MonsterCombatResolver.DamageUpBonusFlatPerIncrement);
    }
}
