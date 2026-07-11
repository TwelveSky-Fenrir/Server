using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneFormationPartyBuffGateTests
{
    private const int FormationSkillId = 76;
    private const int FormationBuffSlot = 2;
    private const int GradeNum1 = 5;
    private const int GradeNum2 = 0;

    private static SkillDefinition FormationSkill(byte maxUpgradePoint = 10, short manaUse = 0,
        byte attackSuccessUp = 15, short runTime = 50)
    {
        var row = new SkillRowDto(FormationSkillId, "Formation", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(FormationSkillId, 0, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0,
            attackSuccessUp, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(FormationSkillId, 1, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0,
            attackSuccessUp, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillDefinition CriticalUpSkill(byte maxUpgradePoint, short manaUse, byte criticalUp,
        short runTime)
    {
        var row = new SkillRowDto(83, "CriticalUp", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(83, 0, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, criticalUp, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(83, 1, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, criticalUp, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static void SeedSkillHotkey(PlayerRuntimeState state, int skillId, int investedGrade, byte page = 0,
        byte index = 0)
    {
        state.Hotkeys = state.Hotkeys.SetItem((page, index),
            new HotkeySlot(HotkeyBindingKind.Skill, skillId, investedGrade));
        state.LearnedSkills = state.LearnedSkills.SetItem(index, new LearnedSkill(skillId, investedGrade));
    }

    private static ActionInfo BaseAction(int sort, int skillNumber, int gradeNum1, int gradeNum2,
        int targetCharacterId = 0)
    {
        return new ActionInfo
        {
            Type = 0, Sort = sort, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = targetCharacterId,
            TargetObjectUniqueNumber = unchecked((int)(uint)targetCharacterId),
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = gradeNum2, SkillValue = 0
        };
    }

        private static ActionInfo PartyBuffCastAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return BaseAction(FormationSkillCatalog.PartyBuffArmActionSort, skillNumber, gradeNum1, gradeNum2);
    }

        private static ActionInfo PartyBuffDoneAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return BaseAction(FormationSkillCatalog.PartyBuffConfirmActionSort, skillNumber, gradeNum1, gradeNum2);
    }

        private static ActionInfo RestAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return BaseAction(0, skillNumber, gradeNum1, gradeNum2);
    }

        private static ActionInfo SkillEffectConfirmAction(int skillNumber, int gradeNum1, int gradeNum2 = 0,
        int targetCharacterId = 0)
    {
        return BaseAction(1, skillNumber, gradeNum1, gradeNum2, targetCharacterId);
    }

    private static ActionInfo SkillCastStartAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return BaseAction(32, skillNumber, gradeNum1, gradeNum2);
    }

        private static void SeedFullPartyPresent(Zone zone, PartyRegistry partyRegistry, int casterId)
    {
        var allyIds = new[] { casterId + 1, casterId + 2, casterId + 3, casterId + 4 };
        foreach (var allyId in allyIds)
        {
            var (session, _) = ZoneTestKit.CreateSession(allyId);
            zone.Post(ZoneCommand.Enter(allyId, ZoneTestKit.EnterData(session, zone.MapId, $"Ally{allyId}")));
        }

        zone.Tick(TimeSpan.FromMilliseconds(50));

        foreach (var allyId in allyIds)
        {
            Assert.Equal(PartyInviteOutcome.Sent, partyRegistry.TryInvite(casterId, 1, 0, allyId, 1, 0));
            Assert.True(partyRegistry.TryAnswer(allyId, true, false, out _, out _, out _));
        }

        Assert.Equal(PartyRegistry.MaxMembers, partyRegistry.GetMembers(casterId).Count);
    }

    [Fact]
    public void FullPartyPresent_AndMarkerDone_WritesTheFormationBuff()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [FormationSkillId] = FormationSkill() }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var partyRegistry = new PartyRegistry();
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData, partyRegistry: partyRegistry);
        var (casterSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(casterSession, 1, "Caster")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        SeedFullPartyPresent(zone, partyRegistry, 10);
        Assert.True(zone.TryGetPlayer(10, out var caster));

        zone.Post(ZoneCommand.Move(10, PartyBuffCastAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.Cast, caster!.PartyBuffAct);

        zone.Post(ZoneCommand.Move(10, PartyBuffDoneAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.Done, caster.PartyBuffAct);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(FormationSkillId, GradeNum1, GradeNum2), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(15, caster.Buffs.Buff[FormationBuffSlot * 2]);
        Assert.Equal(50, caster.Buffs.Buff[FormationBuffSlot * 2 + 1]);
        Assert.Equal(PartyBuffAction.None, caster.PartyBuffAct);
    }

    [Fact]
    public void FullPartyPresent_ButMarkerStillCast_NotYetDone_BuffNotWritten()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [FormationSkillId] = FormationSkill() }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var partyRegistry = new PartyRegistry();
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData, partyRegistry: partyRegistry);
        var (casterSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(casterSession, 1, "Caster")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        SeedFullPartyPresent(zone, partyRegistry, 10);
        Assert.True(zone.TryGetPlayer(10, out var caster));

        zone.Post(ZoneCommand.Move(10, PartyBuffCastAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.Cast, caster!.PartyBuffAct);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(FormationSkillId, GradeNum1, GradeNum2), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, caster.Buffs.Buff[FormationBuffSlot * 2]);
        Assert.Equal(0, caster.Buffs.Buff[FormationBuffSlot * 2 + 1]);
        Assert.Equal(PartyBuffAction.Cast, caster.PartyBuffAct);
    }

    [Fact]
    public void FullPartyPresent_ButMarkerNeverArmed_None_BuffNotWritten()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [FormationSkillId] = FormationSkill() }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var partyRegistry = new PartyRegistry();
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData, partyRegistry: partyRegistry);
        var (casterSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(casterSession, 1, "Caster")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        SeedFullPartyPresent(zone, partyRegistry, 10);
        Assert.True(zone.TryGetPlayer(10, out var caster));

        zone.Post(ZoneCommand.Move(10, RestAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.None, caster!.PartyBuffAct);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(FormationSkillId, GradeNum1, GradeNum2), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, caster.Buffs.Buff[FormationBuffSlot * 2]);
        Assert.Equal(PartyBuffAction.None, caster.PartyBuffAct);
    }

    [Fact]
    public void MarkerDone_ButPartyNotFull_BuffNotWritten()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [FormationSkillId] = FormationSkill() }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (casterSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(casterSession, 1, "Caster")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var caster));

        zone.Post(ZoneCommand.Move(10, PartyBuffCastAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, PartyBuffDoneAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.Done, caster!.PartyBuffAct);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(FormationSkillId, GradeNum1, GradeNum2), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, caster.Buffs.Buff[FormationBuffSlot * 2]);
    }

    [Fact]
    public void NonFormationSkill_WritesRegardlessOfPartyOrMarker()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [83] = CriticalUpSkill(10, 0, 12, 40) }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (casterSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(casterSession, 1, "Caster")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var caster));
        SeedSkillHotkey(caster!, 83, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(83, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(83, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(12, caster!.Buffs.Buff[10 * 2]);
        Assert.Equal(40, caster.Buffs.Buff[10 * 2 + 1]);
        Assert.Equal(PartyBuffAction.None, caster.PartyBuffAct);
    }
}
