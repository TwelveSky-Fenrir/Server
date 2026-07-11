using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Tests.AntiCheat;

/// <summary>
///     Covers <see cref="SkillCastGuard.Evaluate" /> — the hotkey/learned-slot match, the client-bonus-grade
///     consistency check, and the "Skill Hack1" grade-bound check
///     (Server/ts25zone/S04_MyWork02.cpp:1560-1722), plus the two deliberate hardening choices (uniform
///     disconnect on a bonus mismatch; validate-before-mutate so this guard never charges mana).
/// </summary>
public class SkillCastGuardTests
{
    private const int SkillNumber = 41;
    private const int InvestedGrade = 3;
    private const int BonusGrade = 2;

    private static ImmutableDictionary<(byte Page, byte Index), HotkeySlot> HotkeyBoundTo(
        int skillNumber, int investedGrade)
    {
        return ImmutableDictionary<(byte, byte), HotkeySlot>.Empty
            .Add((0, 5), new HotkeySlot(HotkeyBindingKind.Item, 9001, 1)) // noise: a non-skill binding
            .Add((1, 2), new HotkeySlot(HotkeyBindingKind.Skill, skillNumber, investedGrade));
    }

    private static ImmutableDictionary<byte, LearnedSkill> LearnedWith(int skillNumber)
    {
        return ImmutableDictionary<byte, LearnedSkill>.Empty
            .Add(0, new LearnedSkill(999, 1))
            .Add(4, new LearnedSkill(skillNumber, 5));
    }

    private static SkillCastGuardContext Context(
        int category,
        bool isAutoState = false,
        int claimedSkill = SkillNumber,
        int claimedInvested = InvestedGrade,
        int claimedBonus = BonusGrade,
        int serverBonus = BonusGrade,
        int serverMax = 5,
        bool isRealSkillCast = true,
        ImmutableDictionary<(byte Page, byte Index), HotkeySlot>? hotkeys = null,
        ImmutableDictionary<byte, LearnedSkill>? learned = null)
    {
        return new SkillCastGuardContext(category, isAutoState, claimedSkill, claimedInvested, claimedBonus,
            serverBonus, serverMax, isRealSkillCast,
            hotkeys ?? HotkeyBoundTo(SkillNumber, InvestedGrade),
            learned ?? LearnedWith(SkillNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void NonValidatedCategories_AlwaysAllowed(int category)
    {
        // Codes 0/3 fall through with no skill guard, even with a wildly inconsistent claim.
        var context = Context(category, claimedSkill: 12345, claimedInvested: 999, claimedBonus: 999,
            serverBonus: 0, serverMax: 0);
        Assert.Equal(SkillCastOffense.None, SkillCastGuard.Evaluate(context));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CategoryOneAndTwo_MatchingHotkey_ConsistentBonus_Allowed(int category)
    {
        Assert.Equal(SkillCastOffense.None, SkillCastGuard.Evaluate(Context(category)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void HotkeyMismatch_Disconnects(int category)
    {
        // Bound hotkey exists but for a different skill number.
        var context = Context(category, hotkeys: HotkeyBoundTo(SkillNumber + 1, InvestedGrade));
        Assert.Equal(SkillCastOffense.HotkeyMismatch, SkillCastGuard.Evaluate(context));
    }

    [Fact]
    public void HotkeyMatch_RequiresBothSkillNumberAndGrade()
    {
        // Right skill, wrong invested grade on the bound slot → no match.
        var context = Context(1, hotkeys: HotkeyBoundTo(SkillNumber, InvestedGrade + 1));
        Assert.Equal(SkillCastOffense.HotkeyMismatch, SkillCastGuard.Evaluate(context));
    }

    [Fact]
    public void CategoryTwoAutoState_UsesLearnedSlots_PresentAllows()
    {
        var context = Context(2, isAutoState: true, hotkeys: ImmutableDictionary<(byte, byte), HotkeySlot>.Empty);
        Assert.Equal(SkillCastOffense.None, SkillCastGuard.Evaluate(context));
    }

    [Fact]
    public void CategoryTwoAutoState_LearnedSkillMissing_Disconnects()
    {
        var context = Context(2, isAutoState: true, learned: LearnedWith(SkillNumber + 100),
            hotkeys: ImmutableDictionary<(byte, byte), HotkeySlot>.Empty);
        Assert.Equal(SkillCastOffense.LearnedSkillMissing, SkillCastGuard.Evaluate(context));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BonusGradeMismatch_UniformlyDisconnects_BothCategories(int category)
    {
        // Client claims a bonus that disagrees with server state (under-claim) — legacy silently aborted this
        // for category 2; Fenrir disconnects uniformly.
        var context = Context(category, claimedBonus: 1, serverBonus: 2);
        Assert.Equal(SkillCastOffense.BonusGradeMismatch, SkillCastGuard.Evaluate(context));
    }

    [Fact]
    public void SkillHack1_InvestedGradeOverServerMax_Disconnects()
    {
        // Bonus matches so the equality gate passes; invested grade exceeds the server max → Skill Hack1.
        var context = Context(1, claimedInvested: 6, serverMax: 5, claimedBonus: 2, serverBonus: 2);
        Assert.Equal(SkillCastOffense.SkillHack1, SkillCastGuard.Evaluate(context));
    }

    [Fact]
    public void SkillHack1_NotAppliedToSpecialCaseCast()
    {
        // isRealSkillCast=false models the enumerated buff/party-buff/bottle exclusion: the bound check is skipped.
        var context = Context(1, claimedInvested: 6, serverMax: 5, isRealSkillCast: false);
        Assert.Equal(SkillCastOffense.None, SkillCastGuard.Evaluate(context));
    }

    [Fact]
    public void InvestedGradeAtServerMax_IsAllowed()
    {
        var context = Context(1, claimedInvested: 5, serverMax: 5);
        Assert.Equal(SkillCastOffense.None, SkillCastGuard.Evaluate(context));
    }
}
