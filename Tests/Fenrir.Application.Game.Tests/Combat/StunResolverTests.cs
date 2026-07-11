using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

public class StunResolverTests
{
    private static CombatantSnapshot Combatant(int characterId, byte tribe, bool isDead = false,
        TimeSpan? zoneEntryAt = null)
    {
        return new CombatantSnapshot(characterId, tribe, isDead, 1000, 1000, 0, 0, 0, zoneEntryAt, default, 0);
    }

    private static SkillDefinition FlatSkill(int skillId, int stunAttack = 0, int stunDefense = 0, int runTime = 0)
    {
        var skill = WorldDataTestRows.Skill(skillId) with { MaxUpgradePoint = 20 };
        var grade0 = WorldDataTestRows.SkillGrade(skillId, 0) with
        {
            StunAttack = (byte)stunAttack, StunDefense = (byte)stunDefense, RunTime = (short)runTime
        };
        var grade1 = WorldDataTestRows.SkillGrade(skillId, 1) with
        {
            StunAttack = (byte)stunAttack, StunDefense = (byte)stunDefense, RunTime = (short)runTime
        };
        return new SkillDefinition(skill, [], [grade0, grade1]);
    }

    private static StunAttemptRequest Request(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        SkillDefinition? usedSkill,
        int usedSkillId = 7,
        int usedSkillGradePoints = 10,
        bool zoneAllowsEnemyTribeAttack = true,
        bool sharesDuel = false,
        int defenderActionSort = 2,
        bool defenderPshopOpen = false,
        bool echoFlag = false,
        int attackerAnimatingSkillNumber = 0,
        int attackerAnimatingGradePoints = 0,
        SkillDefinition? resistSkill = null,
        int resistGradePoints = 0,
        bool defenderHasImmunityBuff = false)
    {
        return new StunAttemptRequest(attacker, defender, defenderActionSort, defenderPshopOpen,
            zoneAllowsEnemyTribeAttack, sharesDuel, echoFlag, usedSkillId, usedSkillGradePoints,
            attackerAnimatingSkillNumber, attackerAnimatingGradePoints, usedSkill, resistSkill, resistGradePoints,
            defenderHasImmunityBuff);
    }

    [Fact]
    public void AttackerDead_IsRejected()
    {
        var attacker = Combatant(1, 0, true);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(Request(attacker, defender, FlatSkill(7, 50, runTime: 5)),
            TimeSpan.Zero, new ScriptedRandomSource(0));
        Assert.True(outcome.Rejected);
        Assert.Equal(StunRejectReason.AttackerDead, outcome.RejectReason);
    }

    [Fact]
    public void AttackerWithinProtectWindow_IsRejected()
    {
        var attacker = Combatant(1, 0, zoneEntryAt: TimeSpan.FromSeconds(5));
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(Request(attacker, defender, FlatSkill(7, 50, runTime: 5)),
            TimeSpan.FromSeconds(6), new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.AttackerProtected, outcome.RejectReason);
    }

    [Fact]
    public void DefenderDead_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1, true);
        var outcome = StunResolver.Resolve(Request(attacker, defender, FlatSkill(7, 50, runTime: 5)),
            TimeSpan.Zero, new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.DefenderDead, outcome.RejectReason);
    }

    [Fact]
    public void DefenderShopOpen_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5), defenderPshopOpen: true), TimeSpan.Zero,
            new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.DefenderShopOpen, outcome.RejectReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void DefenderActionState_NoActionYetOrDeathPose_BlocksTargeting(int sort)
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5), defenderActionSort: sort), TimeSpan.Zero,
            new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.DefenderActionStateBlocksTargeting, outcome.RejectReason);
    }

    [Fact]
    public void DefenderWithinProtectWindow_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1, zoneEntryAt: TimeSpan.FromSeconds(5));
        var outcome = StunResolver.Resolve(Request(attacker, defender, FlatSkill(7, 50, runTime: 5)),
            TimeSpan.FromSeconds(6), new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.DefenderProtected, outcome.RejectReason);
    }

    [Fact]
    public void SameTribe_NoZonePvp_NoDuel_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 0);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5), zoneAllowsEnemyTribeAttack: true,
                sharesDuel: false), TimeSpan.Zero, new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.NotAuthorized, outcome.RejectReason);
    }

    [Fact]
    public void DifferentTribe_ZoneDisallowsPvp_NoDuel_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5), zoneAllowsEnemyTribeAttack: false,
                sharesDuel: false), TimeSpan.Zero, new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.NotAuthorized, outcome.RejectReason);
    }

    [Fact]
    public void SameTribe_MatchedDuel_IsAuthorized()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 0);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5), zoneAllowsEnemyTribeAttack: false,
                sharesDuel: true), TimeSpan.Zero, new ScriptedRandomSource(10));
        Assert.False(outcome.Rejected);
    }

    [Fact]
    public void AntiCheatEcho_Enforced_WhenFlagSet_AndMismatched()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5),
                echoFlag: true, attackerAnimatingSkillNumber: 999, attackerAnimatingGradePoints: 10), TimeSpan.Zero,
            new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.AntiCheatEchoMismatch, outcome.RejectReason);
    }

    [Fact]
    public void AntiCheatEcho_Skipped_WhenFlagNotSet_EvenIfMismatched()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5),
                echoFlag: false, attackerAnimatingSkillNumber: 999, attackerAnimatingGradePoints: 10), TimeSpan.Zero,
            new ScriptedRandomSource(0));
        Assert.False(outcome.Rejected);
    }

    [Fact]
    public void UsedSkillUnknown_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(Request(attacker, defender, null), TimeSpan.Zero,
            new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.NoStunSuccessValue, outcome.RejectReason);
    }

    [Fact]
    public void StunAttackValueBelowOne_IsRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(Request(attacker, defender, FlatSkill(7, runTime: 5)), TimeSpan.Zero,
            new ScriptedRandomSource(0));
        Assert.Equal(StunRejectReason.NoStunSuccessValue, outcome.RejectReason);
    }

    [Fact]
    public void SuccessfulStun_ResolvesDurationFromRunTimeFactor_AndIsNotRejected()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(Request(attacker, defender, FlatSkill(7, 50, runTime: 8)), TimeSpan.Zero,
            new ScriptedRandomSource(10));
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Success);
        Assert.Equal(8, outcome.ResolvedDurationSeconds);
        Assert.False(outcome.IsTeamStunSkill);
    }

    [Fact]
    public void RollLosesToDefenderStunResistSkill_IsFailure_NotRejection()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var resist = FlatSkill(5, stunDefense: 999);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5), resistSkill: resist, resistGradePoints: 10),
            TimeSpan.Zero, new ScriptedRandomSource(0));
        Assert.False(outcome.Rejected);
        Assert.False(outcome.Success);
    }

    [Fact]
    public void SuccessfulRoll_ButZeroResolvedDuration_IsTreatedAsFailure_NotSuccess()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(Request(attacker, defender, FlatSkill(7, 50, runTime: 0)), TimeSpan.Zero,
            new ScriptedRandomSource(0));
        Assert.False(outcome.Rejected);
        Assert.False(outcome.Success);
    }

    [Fact]
    public void TeamStunSkill_RollsOutOfOneHundred_NotFiveHundred()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var skill = FlatSkill(StunResolver.TeamStunSkillId, 10, runTime: 5);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, skill, StunResolver.TeamStunSkillId),
            TimeSpan.Zero, new ScriptedRandomSource(60));
        Assert.False(outcome.Success);
    }

    [Fact]
    public void TeamStunSkill_ForcesDefenderBlockToZero_RegardlessOfEquippedResistSkill()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var skill = FlatSkill(StunResolver.TeamStunSkillId, 10, runTime: 5);
        var resist = FlatSkill(5, stunDefense: 999);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, skill, StunResolver.TeamStunSkillId, resistSkill: resist,
                resistGradePoints: 10), TimeSpan.Zero, new ScriptedRandomSource(5));
        Assert.True(outcome.Success);
        Assert.True(outcome.IsTeamStunSkill);
    }

    [Fact]
    public void TeamStunSkill_BypassesStunImmunityBuff()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var skill = FlatSkill(StunResolver.TeamStunSkillId, 10, runTime: 5);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, skill, StunResolver.TeamStunSkillId,
                defenderHasImmunityBuff: true), TimeSpan.Zero, new ScriptedRandomSource(5));
        Assert.True(outcome.Success);
    }

    [Fact]
    public void DefenderStunDefenseBuff_AppliesFlatBlockOfOneHundred_LowSuccessValueStillFails()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 50, runTime: 5), defenderHasImmunityBuff: true),
            TimeSpan.Zero, new ScriptedRandomSource(0));
        Assert.False(outcome.Rejected);
        Assert.False(outcome.Success);
    }

    [Fact]
    public void DefenderStunDefenseBuff_IsFiniteMitigation_NotAbsoluteImmunity_HighSuccessValueStillLands()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 500, runTime: 5), defenderHasImmunityBuff: true),
            TimeSpan.Zero, new ScriptedRandomSource(0));
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Success);
    }

    [Fact]
    public void DefenderStunDefenseBuff_ReplacesNotAddsTo_ResistSkillDerivedBlock()
    {
        var attacker = Combatant(1, 0);
        var defender = Combatant(2, 1);
        var resist = FlatSkill(5, stunDefense: 999);
        var outcome = StunResolver.Resolve(
            Request(attacker, defender, FlatSkill(7, 150, runTime: 5), resistSkill: resist, resistGradePoints: 10,
                defenderHasImmunityBuff: true),
            TimeSpan.Zero, new ScriptedRandomSource(0));
        Assert.False(outcome.Rejected);
        Assert.True(outcome.Success);
    }
}
