using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Simulation;

public class BuffDurationUnitConventionTests
{
    private const int SkillCasterCharacterId = 10;
    private const int PotionUserCharacterId = 20;
    private const int SkillCastBuffSlot = 8;
    private const int AssassinScrollSlot = 15;
    private const int HitRateBookSlot = 17;

    [Fact]
    public void SkillCastBuffAndAssassinScrollBuff_ConfiguredForTheSame40SecondDuration_ExpireOnTheSameGateOpening()
    {
        AssertSkillCastAndPotionBuffsExpireTogether(configuredDuration: 40, potionType1: 12, potionSlot: AssassinScrollSlot);
    }

    [Fact]
    public void SkillCastBuffAndHitRateBookBuff_ConfiguredForTheSame60SecondDuration_ExpireOnTheSameGateOpening()
    {
        AssertSkillCastAndPotionBuffsExpireTogether(configuredDuration: 60, potionType1: 14, potionSlot: HitRateBookSlot);
    }

    private static void AssertSkillCastAndPotionBuffsExpireTogether(int configuredDuration, int potionType1,
        int potionSlot)
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);

        var (skillCasterSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(SkillCasterCharacterId, ZoneTestKit.EnterData(skillCasterSession, 1)));
        var (potionUserSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(PotionUserCharacterId, ZoneTestKit.EnterData(potionUserSession, 1, "Hero2")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(SkillCasterCharacterId, out var skillCaster));
        Assert.True(zone.TryGetPlayer(PotionUserCharacterId, out var potionUser));

        var skillResult = SkillCastResolver.TryCast(BuildSelfBuffSkill((short)configuredDuration), gradePoints: 5,
            casterMana: 0, casterMaxLife: 1000, equippedWeaponSort: null, supportSkillTimeUpRatio: 1);
        var skillWrite = Assert.Single(skillResult.BuffWrites);
        Assert.Equal(SkillCastBuffSlot, skillWrite.Slot);
        Assert.Equal(configuredDuration, skillWrite.DurationTicks);

        var potionResult = HotkeyItemConsumptionResolver.Resolve(
            0, 0, new HotkeySlot(HotkeyBindingKind.Item, 12345, 3),
            false, false, true,
            true, HotkeyItemConsumptionResolver.ConsumableItemCategory, potionType1, 0,
            100, 100, 100, 100,
            true, 0,
            false, 0, false, 0);
        var potionWrite = Assert.Single(potionResult.BuffWrites);
        Assert.Equal(configuredDuration, potionWrite.DurationTicks);
        Assert.Equal(potionSlot, potionWrite.Slot);

        skillCaster!.Buffs.Buff[skillWrite.Slot * 2] = skillWrite.Value;
        skillCaster.Buffs.Buff[skillWrite.Slot * 2 + 1] = skillWrite.DurationTicks;
        potionUser!.Buffs.Buff[potionWrite.Slot * 2] = potionWrite.Value;
        potionUser.Buffs.Buff[potionWrite.Slot * 2 + 1] = potionWrite.DurationTicks;

        var openingsRemaining = configuredDuration;
        var almostEveryOpening = SimulationClock.ToTimeSpan((openingsRemaining - 1) * SimulationClock.OneSecondGateLegacyTicks);
        zone.Tick(almostEveryOpening);

        Assert.Equal(1, skillCaster.Buffs.Buff[skillWrite.Slot * 2 + 1]);
        Assert.Equal(1, potionUser.Buffs.Buff[potionWrite.Slot * 2 + 1]);

        zone.Tick(SimulationClock.ToTimeSpan(SimulationClock.OneSecondGateLegacyTicks));

        Assert.Equal(0, skillCaster.Buffs.Buff[skillWrite.Slot * 2]);
        Assert.Equal(0, skillCaster.Buffs.Buff[skillWrite.Slot * 2 + 1]);
        Assert.Equal(0, potionUser.Buffs.Buff[potionWrite.Slot * 2]);
        Assert.Equal(0, potionUser.Buffs.Buff[potionWrite.Slot * 2 + 1]);
    }

    private static SkillDefinition BuildSelfBuffSkill(short runTime)
    {
        var row = new SkillRowDto(
            SkillId: 6,
            Name: "Test",
            Type: 0,
            AttackType: 0,
            DataNumber2D: 0,
            TribeInfo1: 0,
            TribeInfo2: 0,
            LearnSkillPoint: 0,
            MaxUpgradePoint: 10,
            TotalHitNumber: 1,
            ValidRadius: 0);

        var grade0 = BuildGrade(0, runTime);
        var grade1 = BuildGrade(1, runTime);

        return new SkillDefinition(row, [], [grade0, grade1]);
    }

    private static SkillGradeRowDto BuildGrade(byte gradeIndex, short runTime)
    {
        return new SkillGradeRowDto(
            SkillId: 6,
            GradeIndex: gradeIndex,
            ManaUse: 0,
            RecoverInfo1: 0,
            RecoverInfo2: 0,
            StunAttack: 0,
            StunDefense: 0,
            FastRunSpeed: 0,
            AttackInfo1: 0,
            AttackInfo2: 0,
            AttackInfo3: 0,
            RunTime: runTime,
            ChargingDamageUp: 5,
            AttackPowerUp: 0,
            DefensePowerUp: 0,
            AttackSuccessUp: 0,
            AttackBlockUp: 0,
            ElementAttackUp: 0,
            ElementDefenseUp: 0,
            AttackSpeedUp: 0,
            RunSpeedUp: 0,
            ShieldLifeUp: 0,
            LuckUp: 0,
            CriticalUp: 0,
            ReturnSuccessUp: 0,
            StunDefenseUp: 0,
            DestroySuccessUp: 0);
    }
}
