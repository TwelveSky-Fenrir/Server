using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers D2 hook 4 end-to-end: <c>Zone.EvaluateSkillCastTamperGuard</c>, wired into
///     <c>Zone.HandleMove</c> immediately ahead of <c>Zone.ApplySkillCastManaCharge</c> for every accepted op15
///     category-2 real skill-cast action (Sorts 32/33/38-90). Complements
///     <c>Tests.AntiCheat.SkillCastGuardTests</c> (the guard's own pure-function unit tests) by proving the
///     wiring itself: a tripped offense disconnects the session AND queues an audit-log row, and a legitimate
///     cast is never disturbed.
/// </summary>
public class ZoneSkillCastTamperGuardTests
{
    private static SkillDefinition HolyShieldSkill(byte maxUpgradePoint, short manaUse, byte shieldPercent,
        short runTime)
    {
        var row = new SkillRowDto(82, "Holy Shield", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(82, 0, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            shieldPercent, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(82, 1, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            shieldPercent, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, [], [grade0, grade1]);
    }

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
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var eventLog = new FakeEventLogQueue();
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData, eventLogQueue: eventLog);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, session, state!, eventLog);
    }

    [Fact]
    public void NoMatchingHotkey_DisconnectsSession_AndQueuesAnAuditLogRow()
    {
        var (zone, session, state, eventLog) = SetUp();
        var manaBefore = state.Mana;

        // No hotkey seeded at all -- a bare ZoneTestKit-registered avatar has an empty hotkey table.
        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        // Mana is never charged: the guard runs strictly before ApplySkillCastManaCharge.
        Assert.Equal(manaBefore, state.Mana);

        var row = Assert.Single(eventLog.Enqueued);
        Assert.Equal((short)SkillCastOffense.HotkeyMismatch, row.EventCode);
        Assert.Equal((byte)EventLogCategory.AntiCheat, row.Category);
        Assert.Equal(10, row.ActorCharacterId);
    }

    [Fact]
    public void MatchingHotkeyAndZeroClaimedBonusGrade_CastProceedsNormally_NoDisconnectNoAuditLog()
    {
        var (zone, session, state, eventLog) = SetUp();
        var manaBefore = state.Mana;
        state.Hotkeys = state.Hotkeys.SetItem((0, 0), new HotkeySlot(HotkeyBindingKind.Skill, 82, 10));
        // D2 special-case exclusion set (wave15): skill 82 is not a Formation/Bottle skill, so this is now a
        // "real" skill cast for the SkillHack1 bound check -- a matching learned-skill slot at the same
        // invested grade is required, or GetMaxSkillGradeNum's -1 "not learned" sentinel would trip it.
        state.LearnedSkills = state.LearnedSkills.SetItem(0, new LearnedSkill(82, 10));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(manaBefore - 30, state.Mana); // mana charge still applies once the guard passes
        Assert.Empty(eventLog.Enqueued);
    }

    /// <summary>
    ///     A claimed item-bonus grade (<c>aSkillGradeNum2</c>) that disagrees with the server-computed
    ///     <c>SkillGradeAuthority.GetBonusSkillValue</c> (0 here -- no equipment seeded) is rejected even
    ///     though the hotkey itself matches -- the two checks are independent gates, not a single combined one.
    /// </summary>
    [Fact]
    public void MatchingHotkeyButClaimedBonusGradeMismatch_DisconnectsSession_AndQueuesAnAuditLogRow()
    {
        var (zone, session, state, eventLog) = SetUp();
        state.Hotkeys = state.Hotkeys.SetItem((0, 0), new HotkeySlot(HotkeyBindingKind.Skill, 82, 10));

        // Claims a +5 item bonus with no equipment anywhere backing it.
        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10, 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        var row = Assert.Single(eventLog.Enqueued);
        Assert.Equal((short)SkillCastOffense.BonusGradeMismatch, row.EventCode);
    }

    [Fact]
    public void AutoHunting_WithNoMatchingLearnedSkill_DisconnectsSession_AndQueuesAnAuditLogRow()
    {
        var (zone, session, state, eventLog) = SetUp();
        state.AutoHuntEnabled = true;
        // Hotkey table is irrelevant while auto-hunting -- the guard's auto-state branch checks LearnedSkills
        // instead, which is left empty here.

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        var row = Assert.Single(eventLog.Enqueued);
        Assert.Equal((short)SkillCastOffense.LearnedSkillMissing, row.EventCode);
    }

    [Fact]
    public void AutoHunting_WithMatchingLearnedSkill_CastProceedsNormally()
    {
        var (zone, session, state, eventLog) = SetUp();
        var manaBefore = state.Mana;
        state.AutoHuntEnabled = true;
        // Auto-state branch only checks presence-by-skill#, not grade -- see SkillCastGuard's own remarks.
        state.LearnedSkills = state.LearnedSkills.SetItem(0, new LearnedSkill(82, 10));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Empty(eventLog.Enqueued);
    }

    /// <summary>
    ///     Category-0/3 actions (an ordinary move, Sort 2) never reach the skill-cast tamper guard at all --
    ///     regression guard against accidentally widening the guard's trigger condition beyond category-2
    ///     real skill-cast Sorts.
    /// </summary>
    [Fact]
    public void OrdinaryMove_NeverTriggersTheGuard()
    {
        var (zone, session, state, eventLog) = SetUp();

        // Same position as spawn (100, 0, 100) -- a trivially-plausible zero-distance move, same convention as
        // ZoneCharacterMotionWhitelistTests' own "not this test's concern" moves, so MovementRules' speed gate
        // can never be the reason this test observes no position write.
        var moveAction = new ActionInfo
        {
            Type = 0, Sort = 2, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = 0, TargetObjectUniqueNumber = 0,
            SkillNumber = 0, SkillGradeNum1 = 0, SkillGradeNum2 = 0, SkillValue = 0
        };

        zone.Post(ZoneCommand.Move(10, moveAction));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Empty(eventLog.Enqueued);
        Assert.Equal(100f, state.PosX);
    }
}
