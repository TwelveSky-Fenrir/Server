using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the wave15 D2-skillhack1-special-cases contract: <c>Zone.EvaluateSkillCastTamperGuard</c>'s
///     <c>IsRealSkillCast</c> classification, now wired to <see cref="FormationSkillCatalog.IsExemptFromGradeBoundCheck" />
///     instead of the previous hardcoded-<c>false</c> placeholder that disabled the "Skill Hack1" grade-bound
///     sub-check (<see cref="SkillCastOffense.SkillHack1" />) entirely. Complements
///     <c>Fenrir.Application.Game.Tests.Skills.FormationSkillCatalogTests</c> (the classification's own pure
///     unit tests, unaffected by this wiring) and <c>Fenrir.Application.Game.Tests.AntiCheat.SkillCastGuardTests</c>
///     (the guard's own bool-driven unit tests) by proving the end-to-end Zone wiring itself: a genuine skill
///     cast now trips SkillHack1 when tampered, while the six Formation skills (76-81) remain exempt no matter
///     how implausible the claimed grade is.
/// </summary>
public class ZoneSkillHack1SpecialCaseExclusionTests
{
    // Sort 32 resolves to CharacterMotionWhitelist's skill-cast category (SkillCastEffectCategoryCode == 2) for
    // any Type -- the same convention ZoneSkillCastTamperGuardTests/ZoneSkillCastTests already use.
    private static ActionInfo SkillCastStartAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 32, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = 0, TargetObjectUniqueNumber = 0,
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = gradeNum2, SkillValue = 0
        };
    }

    private static (Zone Zone, ZoneClientSession Session, PlayerRuntimeState State, FakeEventLogQueue EventLog)
        SetUp()
    {
        // No skill definitions seeded anywhere in this file: every test here is only concerned with whether
        // EvaluateSkillCastTamperGuard's classification disconnects the session, not with the downstream
        // mana-charge/effect outcome -- SkillCastResolver.TryCast(null, ...) fails closed (FailureReason
        // .UnknownSkill) with no mana charged and no exception, for every skill number exercised below.
        var worldData = ZoneTestKit.EmptyWorldData();
        var eventLog = new FakeEventLogQueue();
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData, eventLogQueue: eventLog);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, session, state!, eventLog);
    }

    /// <summary>
    ///     Regression guard for the fix itself: before this session, <c>IsRealSkillCast</c> was hardcoded
    ///     <c>false</c>, so this exact scenario (an ordinary, non-Formation, non-Bottle skill number whose
    ///     claimed invested grade wildly exceeds the server-known maximum) NEVER tripped SkillHack1. It now
    ///     does -- proving the bound check the D2 workstream introduced is finally live for a genuine cast.
    /// </summary>
    [Fact]
    public void NonExemptSkill_ClaimedGradeFarAboveServerMax_TripsSkillHack1_Disconnects()
    {
        var (zone, session, state, eventLog) = SetUp();

        // Skill 90 is not one of the six Formation skills (76-81) nor the Bottle skill (1) -- an ordinary
        // skill number. No LearnedSkills entry is seeded, so SkillGradeAuthority.GetMaxSkillGradeNum returns
        // -1 ("not learned"); any positive claimed invested grade exceeds that.
        state.Hotkeys = state.Hotkeys.SetItem((0, 0), new HotkeySlot(HotkeyBindingKind.Skill, 90, 50));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(90, 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        var row = Assert.Single(eventLog.Enqueued);
        Assert.Equal((short)SkillCastOffense.SkillHack1, row.EventCode);
    }

    [Theory]
    [InlineData(76)] // party-buff Formation skill
    [InlineData(77)] // party-buff Formation skill
    [InlineData(79)] // party-buff Formation skill
    [InlineData(81)] // party-buff Formation skill
    [InlineData(78)] // empty-case Formation skill
    [InlineData(80)] // empty-case Formation skill
    public void FormationSkill_ClaimedGradeFarAboveServerMax_IsExempt_NoDisconnect(int formationSkillNumber)
    {
        var (zone, session, state, eventLog) = SetUp();

        // Same "no LearnedSkills entry, wildly excessive claimed grade" shape as the non-exempt test above --
        // the only difference is the skill number itself is one of the six Formation skills, which
        // FormationSkillCatalog.IsExemptFromGradeBoundCheck exempts unconditionally, regardless of action-sort.
        state.Hotkeys = state.Hotkeys.SetItem((0, 0),
            new HotkeySlot(HotkeyBindingKind.Skill, formationSkillNumber, 999));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(formationSkillNumber, 999)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Empty(eventLog.Enqueued);
    }

    /// <summary>
    ///     Skill number 0 ("no skill claimed") must never reach the SkillHack1 sub-check regardless of the
    ///     claimed grade value riding alongside it -- <c>IsRealSkillCast</c>'s own <c>action.SkillNumber != 0</c>
    ///     guard, independent from (and checked before) the Formation/Bottle exemption lookup.
    /// </summary>
    [Fact]
    public void SkillNumberZero_NeverTripsSkillHack1_RegardlessOfClaimedGrade()
    {
        var (zone, session, state, eventLog) = SetUp();

        state.Hotkeys = state.Hotkeys.SetItem((0, 0), new HotkeySlot(HotkeyBindingKind.Skill, 0, 999));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(0, 999)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Empty(eventLog.Enqueued);
    }

    /// <summary>
    ///     The Bottle exemption (skill 1, action-sort 16) exists in the D2 contract's primary-handler switch,
    ///     but per the contract's own "Narrowing at Fenrir's currently-existing call site" edge case, action-sort
    ///     16 resolves to <c>CharacterMotionWhitelist</c>'s category 0 (not the skill-cast category 2), so
    ///     <c>Zone.EvaluateSkillCastTamperGuard</c> is never actually invoked for it via the real Zone pipeline --
    ///     this is a fact about category routing, not a gap in the exemption logic. This test instead exercises
    ///     the EXACT classification formula <c>Zone.EvaluateSkillCastTamperGuard</c> applies
    ///     (<c>skillNumber != 0 &amp;&amp; !FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort,
    ///     isPrimaryHandler: true)</c>) directly against <see cref="SkillCastGuard.Evaluate" />, proving the Bottle
    ///     pair is correctly exempt under that formula so the wiring is provably correct for the day the call
    ///     site is ever widened to also run for category-0 packets (the contract's own explicit warning).
    /// </summary>
    [Fact]
    public void BottleExemption_UnderTheExactZoneWiringFormula_IsExempt_ForPrimaryHandlerOnly()
    {
        const int bottleSkillNumber = FormationSkillCatalog.BottleSkillNumber;
        const int bottleActionSort = FormationSkillCatalog.BottleExemptionActionSort;

        var hotkeys = ImmutableDictionary<(byte, byte), HotkeySlot>.Empty
            .Add((0, 0), new HotkeySlot(HotkeyBindingKind.Skill, bottleSkillNumber, 999));
        var learnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty;

        var isRealSkillCastPrimary = bottleSkillNumber != 0 &&
            !FormationSkillCatalog.IsExemptFromGradeBoundCheck(bottleSkillNumber, bottleActionSort,
                isPrimaryHandler: true);
        var isRealSkillCastSecondary = bottleSkillNumber != 0 &&
            !FormationSkillCatalog.IsExemptFromGradeBoundCheck(bottleSkillNumber, bottleActionSort,
                isPrimaryHandler: false);

        Assert.False(isRealSkillCastPrimary); // exempt -- Zone's own call site always passes isPrimaryHandler: true
        Assert.True(isRealSkillCastSecondary); // op16's mirror carries no Bottle exemption -- see FormationSkillCatalog

        // With a wildly excessive claimed grade (999 against a "not learned" -1 server max), the primary-handler
        // formula's SkillCastGuardContext must still resolve to None (exempt), while the secondary-handler
        // formula's equivalent context resolves to SkillHack1 (not exempt) -- proving IsRealSkillCast is exactly
        // what decides the outcome here, with every other input held identical.
        var primaryOffense = SkillCastGuard.Evaluate(new SkillCastGuardContext(
            SkillCategoryCode: 2, IsAutoState: false, ClaimedSkillNumber: bottleSkillNumber,
            ClaimedInvestedGrade: 999, ClaimedBonusGrade: 0, ServerBonusGrade: 0, ServerMaxGrade: -1,
            IsRealSkillCast: isRealSkillCastPrimary, Hotkeys: hotkeys, LearnedSkills: learnedSkills));
        var secondaryOffense = SkillCastGuard.Evaluate(new SkillCastGuardContext(
            SkillCategoryCode: 2, IsAutoState: false, ClaimedSkillNumber: bottleSkillNumber,
            ClaimedInvestedGrade: 999, ClaimedBonusGrade: 0, ServerBonusGrade: 0, ServerMaxGrade: -1,
            IsRealSkillCast: isRealSkillCastSecondary, Hotkeys: hotkeys, LearnedSkills: learnedSkills));

        Assert.Equal(SkillCastOffense.None, primaryOffense);
        Assert.Equal(SkillCastOffense.SkillHack1, secondaryOffense);
    }
}
