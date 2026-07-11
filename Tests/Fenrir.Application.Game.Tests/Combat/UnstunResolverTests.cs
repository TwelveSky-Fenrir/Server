using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

public class UnstunResolverTests
{
    private static CombatantSnapshot Combatant(int characterId, byte tribe, bool isDead = false)
    {
        return new CombatantSnapshot(characterId, tribe, isDead, 1000, 1000, 0, 0, 0, null, default, 0);
    }

    private static SkillDefinition FlatSkill(int skillId, int stunDefense)
    {
        var skill = WorldDataTestRows.Skill(skillId) with { MaxUpgradePoint = 20 };
        var grade0 = WorldDataTestRows.SkillGrade(skillId, 0) with { StunDefense = (byte)stunDefense };
        var grade1 = WorldDataTestRows.SkillGrade(skillId, 1) with { StunDefense = (byte)stunDefense };
        return new SkillDefinition(skill, [], [grade0, grade1]);
    }

    private static UnstunAttemptRequest Request(
        CombatantSnapshot curer,
        CombatantSnapshot target,
        bool targetIsStunned = true,
        bool targetPshopOpen = false,
        bool echoFlag = false,
        int usedSkillId = 5,
        int usedSkillGradePoints = 10,
        int curerAnimatingSkillNumber = 0,
        int curerAnimatingGradePoints = 0,
        SkillDefinition? usedSkill = null)
    {
        return new UnstunAttemptRequest(curer, target, targetIsStunned, targetPshopOpen, echoFlag, usedSkillId,
            usedSkillGradePoints, curerAnimatingSkillNumber, curerAnimatingGradePoints, usedSkill);
    }

    [Fact]
    public void CurerDead_IsRejected()
    {
        var curer = Combatant(1, 0, true);
        var target = Combatant(2, 0);
        var outcome = UnstunResolver.Resolve(Request(curer, target), new ScriptedRandomSource(0));
        Assert.True(outcome.Rejected);
        Assert.Equal(UnstunRejectReason.CurerDead, outcome.RejectReason);
    }

    [Fact]
    public void TargetDead_IsRejected()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0, true);
        var outcome = UnstunResolver.Resolve(Request(curer, target), new ScriptedRandomSource(0));
        Assert.Equal(UnstunRejectReason.TargetDead, outcome.RejectReason);
    }

    [Fact]
    public void TargetShopOpen_IsRejected()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var outcome = UnstunResolver.Resolve(Request(curer, target, targetPshopOpen: true),
            new ScriptedRandomSource(0));
        Assert.Equal(UnstunRejectReason.TargetShopOpen, outcome.RejectReason);
    }

    [Fact]
    public void TargetNotStunned_IsRejected_RegardlessOfGrade()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var outcome = UnstunResolver.Resolve(Request(curer, target, false, usedSkillGradePoints: 20),
            new ScriptedRandomSource(0));
        Assert.Equal(UnstunRejectReason.TargetNotStunned, outcome.RejectReason);
    }

    [Fact]
    public void DifferentTribe_IsRejected_NoDuelExceptionExists()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 1);
        var outcome = UnstunResolver.Resolve(Request(curer, target), new ScriptedRandomSource(0));
        Assert.Equal(UnstunRejectReason.NotAuthorized, outcome.RejectReason);
    }

    [Fact]
    public void EchoFlagSet_UnrecognizedSkillId_IsRejected()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var outcome = UnstunResolver.Resolve(Request(curer, target, echoFlag: true, usedSkillId: 999),
            new ScriptedRandomSource(0));
        Assert.Equal(UnstunRejectReason.UnrecognizedSkill, outcome.RejectReason);
    }

    [Fact]
    public void EchoFlagSet_RecognizedSkillButAnimatingStateMismatch_IsRejected()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var outcome = UnstunResolver.Resolve(
            Request(curer, target, echoFlag: true, usedSkillId: 5, usedSkillGradePoints: 10,
                curerAnimatingSkillNumber: 24, curerAnimatingGradePoints: 10), new ScriptedRandomSource(0));
        Assert.Equal(UnstunRejectReason.AntiCheatEchoMismatch, outcome.RejectReason);
    }

    [Fact]
    public void EchoFlagSet_RecognizedSkillAndMatchingAnimatingState_IsNotRejected()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var outcome = UnstunResolver.Resolve(
            Request(curer, target, echoFlag: true, usedSkillId: 5, usedSkillGradePoints: 20,
                curerAnimatingSkillNumber: 5, curerAnimatingGradePoints: 20), new ScriptedRandomSource(99));
        Assert.False(outcome.Rejected);
    }

    [Fact]
    public void CombinedGrade20OrAbove_IsGuaranteedSuccess_EvenWithUnknownSkill()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var outcome = UnstunResolver.Resolve(Request(curer, target, usedSkillGradePoints: 20, usedSkill: null),
            new ScriptedRandomSource(99));
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Success);
    }

    [Fact]
    public void BelowGuaranteedGrade_UnknownSkill_IsFailure_NotRejection()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var outcome = UnstunResolver.Resolve(Request(curer, target, usedSkillGradePoints: 10, usedSkill: null),
            new ScriptedRandomSource(0));
        Assert.False(outcome.Rejected);
        Assert.False(outcome.Success);
    }

    [Fact]
    public void BelowGuaranteedGrade_RollBeatsDoubledStunDefenseValue_Succeeds()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var skill = FlatSkill(5, 30);
        var outcome = UnstunResolver.Resolve(Request(curer, target, usedSkillGradePoints: 10, usedSkill: skill),
            new ScriptedRandomSource(59));
        Assert.True(outcome.Success);
    }

    [Fact]
    public void BelowGuaranteedGrade_RollLosesToDoubledStunDefenseValue_IsFailure_NotRejection()
    {
        var curer = Combatant(1, 0);
        var target = Combatant(2, 0);
        var skill = FlatSkill(5, 30);
        var outcome = UnstunResolver.Resolve(Request(curer, target, usedSkillGradePoints: 10, usedSkill: skill),
            new ScriptedRandomSource(60));
        Assert.False(outcome.Rejected);
        Assert.False(outcome.Success);
    }
}
