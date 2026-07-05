using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Skills;

/// <summary>
///     Covers <see cref="SkillCastResolver.TryCast" /> against the skill-ID whitelist in
///     <see cref="SkillEffectCatalog" />, ported from <c>MyUtil::ProcessForCreateBuff</c>.
/// </summary>
public class SkillCastResolverTests
{
    private static SkillDefinition BuildSkill(int skillId, byte maxUpgradePoint,
        (int Grade0, int Grade1) manaUse, (int Grade0, int Grade1) runTime, (int Grade0, int Grade1) chargingUp,
        (int Grade0, int Grade1) attackPowerUp = default, (int Grade0, int Grade1) shieldLifeUp = default,
        (int Grade0, int Grade1) recoverLife = default, (int Grade0, int Grade1) recoverMana = default)
    {
        var row = new SkillRowDto(skillId, "Test", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(skillId, 0, (short)manaUse.Grade0, (byte)recoverLife.Grade0,
            (byte)recoverMana.Grade0, 0, 0, 0, 0, 0, 0, (short)runTime.Grade0, (byte)chargingUp.Grade0,
            (byte)attackPowerUp.Grade0, 0, 0, 0, 0, 0, 0, 0, (byte)shieldLifeUp.Grade0, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(skillId, 1, (short)manaUse.Grade1, (byte)recoverLife.Grade1,
            (byte)recoverMana.Grade1, 0, 0, 0, 0, 0, 0, (short)runTime.Grade1, (byte)chargingUp.Grade1,
            (byte)attackPowerUp.Grade1, 0, 0, 0, 0, 0, 0, 0, (byte)shieldLifeUp.Grade1, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    [Fact]
    public void UnknownSkill_Fails()
    {
        var result = SkillCastResolver.TryCast(null, 5, 100, 1000, null);
        Assert.False(result.Success);
        Assert.Equal(SkillCastResolver.FailureReason.UnknownSkill, result.Failure);
    }

    [Fact]
    public void SkillNotInEffectCatalog_Fails()
    {
        var skill = BuildSkill(999, 10, (10, 10), (0, 0), (0, 0));
        var result = SkillCastResolver.TryCast(skill, 5, 100, 1000, null);
        Assert.Equal(SkillCastResolver.FailureReason.NotCastable, result.Failure);
    }

    [Fact]
    public void InsufficientMana_Fails()
    {
        // Skill 6 (Charge) -- ManaUse min=50, max=50 (constant across grades for simplicity).
        var skill = BuildSkill(6, 10, (50, 50), (20, 20), (30, 30));
        var result = SkillCastResolver.TryCast(skill, 5, 10, 1000, null);
        Assert.Equal(SkillCastResolver.FailureReason.InsufficientMana, result.Failure);
    }

    [Fact]
    public void Skill6Charge_WritesBuffSlot8_NoWeaponGate()
    {
        var skill = BuildSkill(6, 10, (50, 50), (20, 20), (10, 90));
        var result = SkillCastResolver.TryCast(skill, 10, 100, 1000, null); // full grade points -> max value
        Assert.True(result.Success);
        Assert.Equal(50, result.ManaCost);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(8, write.Slot);
        Assert.Equal(90, write.Value);
        Assert.Equal(20, write.DurationTicks);
    }

    [Fact]
    public void Skill15AttackPowerUp_RequiresAtkClassWeapon_RejectsWithoutIt()
    {
        var skill = BuildSkill(15, 10, (10, 10), (5, 5), default, (10, 50));
        var result = SkillCastResolver.TryCast(skill, 10, 100, 1000, 13 /* "def" class, not atk */);
        Assert.Equal(SkillCastResolver.FailureReason.WrongWeaponClass, result.Failure);
    }

    [Fact]
    public void Skill15AttackPowerUp_WithAtkClassWeapon_Succeeds()
    {
        var skill = BuildSkill(15, 10, (10, 10), (5, 5), default, (10, 50));
        var result = SkillCastResolver.TryCast(skill, 10, 100, 1000, 14 /* "atk" class */);
        Assert.True(result.Success);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(0, write.Slot);
        Assert.Equal(50, write.Value);
    }

    [Fact]
    public void Skill15AttackPowerUp_NoWeaponEquipped_Rejects()
    {
        var skill = BuildSkill(15, 10, (10, 10), (5, 5), default, (10, 50));
        var result = SkillCastResolver.TryCast(skill, 10, 100, 1000, null);
        Assert.Equal(SkillCastResolver.FailureReason.WrongWeaponClass, result.Failure);
    }

    [Fact]
    public void Skill82HolyShield_ValueIsPercentOfCasterMaxLife()
    {
        var skill = BuildSkill(82, 10, (10, 10), (5, 5), default, shieldLifeUp: (10, 20)); // 20% of MaxLife
        var result = SkillCastResolver.TryCast(skill, 10, 100, 5000, null);
        Assert.True(result.Success);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(9, write.Slot);
        Assert.Equal(1000, write.Value); // 20% of 5000
    }

    [Fact]
    public void Skill106HealLife_ReturnsRawHealAmount()
    {
        var skill = BuildSkill(106, 10, (10, 10), (0, 0), default, recoverLife: (50, 200));
        var result = SkillCastResolver.TryCast(skill, 10, 100, 1000, null);
        Assert.True(result.Success);
        Assert.Equal(SkillEffectKind.HealLife, result.Kind);
        Assert.Equal(200, result.HealAmount);
        Assert.Empty(result.BuffWrites);
    }

    [Fact]
    public void Skill107HealMana_ReturnsRawHealAmount()
    {
        var skill = BuildSkill(107, 10, (10, 10), (0, 0), default, recoverMana: (20, 200));
        var result = SkillCastResolver.TryCast(skill, 10, 100, 1000, null);
        Assert.Equal(SkillEffectKind.HealMana, result.Kind);
        Assert.Equal(200, result.HealAmount);
    }
}
