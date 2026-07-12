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

public class ZoneSkillHack1SpecialCaseExclusionTests
{
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
        var worldData = ZoneTestKit.EmptyWorldData();
        var eventLog = new FakeEventLogQueue();
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData, eventLogQueue: eventLog);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, session, state!, eventLog);
    }

    [Fact]
    public void NonExemptSkill_ClaimedGradeFarAboveServerMax_TripsSkillHack1_Disconnects()
    {
        var (zone, session, state, eventLog) = SetUp();

        state.Hotkeys = state.Hotkeys.SetItem((0, 0), new HotkeySlot(HotkeyBindingKind.Skill, 90, 50));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(90, 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
        var row = Assert.Single(eventLog.Enqueued);
        Assert.Equal((short)SkillCastOffense.SkillHack1, row.EventCode);
    }

    [Theory]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(79)]
    [InlineData(81)]
    [InlineData(78)]
    [InlineData(80)]
    public void FormationSkill_ClaimedGradeFarAboveServerMax_IsExempt_NoDisconnect(int formationSkillNumber)
    {
        var (zone, session, state, eventLog) = SetUp();

        state.Hotkeys = state.Hotkeys.SetItem((0, 0),
            new HotkeySlot(HotkeyBindingKind.Skill, formationSkillNumber, 999));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(formationSkillNumber, 999)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Empty(eventLog.Enqueued);
    }

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

    [Fact]
    public void BottleExemption_UnderTheExactZoneWiringFormula_IsExempt_ForPrimaryHandlerOnly()
    {
        const int bottleSkillNumber = FormationSkillCatalog.BottleSkillNumber;
        const int bottleActionSort = FormationSkillCatalog.BottleExemptionActionSort;

        var hotkeys = ImmutableDictionary<(byte, byte), HotkeySlot>.Empty
            .Add((0, 0), new HotkeySlot(HotkeyBindingKind.Skill, bottleSkillNumber, 999));
        var learnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty;

        var isRealSkillCastPrimary = bottleSkillNumber != 0 &&
                                     !FormationSkillCatalog.IsExemptFromGradeBoundCheck(bottleSkillNumber,
                                         bottleActionSort,
                                         true);
        var isRealSkillCastSecondary = bottleSkillNumber != 0 &&
                                       !FormationSkillCatalog.IsExemptFromGradeBoundCheck(bottleSkillNumber,
                                           bottleActionSort,
                                           false);

        Assert.False(isRealSkillCastPrimary);
        Assert.True(isRealSkillCastSecondary);

        var primaryVerdict = SkillCastGuard.Evaluate(new SkillCastGuardContext(
            2, false, bottleSkillNumber,
            999, 0, 0, -1,
            isRealSkillCastPrimary, hotkeys, learnedSkills));
        var secondaryVerdict = SkillCastGuard.Evaluate(new SkillCastGuardContext(
            2, false, bottleSkillNumber,
            999, 0, 0, -1,
            isRealSkillCastSecondary, hotkeys, learnedSkills));

        Assert.Equal(SkillCastVerdict.Passed, primaryVerdict);
        Assert.Equal(SkillCastOffense.SkillHack1, secondaryVerdict.Offense);
        Assert.Equal(SkillCastEnforcement.Disconnect, secondaryVerdict.Enforcement);
    }
}
