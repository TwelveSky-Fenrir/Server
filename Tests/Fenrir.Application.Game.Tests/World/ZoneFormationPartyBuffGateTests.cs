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

/// <summary>
///     Covers the AND-combined formation-buff broadcast gate in <c>Zone.ApplySkillEffectConfirm</c>: a
///     Formation party-buff skill (76/77/79/81, <see cref="SkillCastResolver.Result.RequiresFullParty" />) now
///     writes its buff only when BOTH the caster's own party has an exactly-full 5 members present in this zone
///     (<c>Zone.HasFullPartyPresent</c>) AND <see cref="PlayerRuntimeState.PartyBuffAct" /> has already reached
///     <see cref="PartyBuffAction.Done" /> -- stricter than either gate alone, an explicit project-owner
///     resolution of the open design question <see cref="PartyBuffMarkerDispatchRules" />'s own remarks
///     describe as three-times-flagged (original B14 pass -&gt; wave9 reconciliation -&gt; a fresh confirmation
///     pass). Drives the real CAST(64)-&gt;DONE(65)-&gt;CONFIRM(1) handshake through a live <see cref="Zone" />
///     via <see cref="ZoneTestKit" /> rather than asserting on the pure catalogs alone (see
///     <c>Skills.PartyBuffMarkerDispatchRulesTests</c> for that side's own coverage of the opcode/Sort
///     reachability rules this drives through) -- the AND-combine itself only lives in
///     <c>Zone.PlayerLifecycle.cs</c> and has no pure-catalog surface of its own to unit-test.
/// </summary>
public class ZoneFormationPartyBuffGateTests
{
    // Skill 76 (AttackSuccessUp, buff slot 2, SkillEffectCatalog.AddPartyBuffAsSelf) -- any of 76/77/79/81 would
    // do; 76 is the simplest to build a SkillGradeRowDto for.
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

    // Non-formation control skill: 83 (CriticalUp, buff slot 10) has RequiresFullParty == false and never
    // touches PartyBuffAct at all -- SkillEffectCatalog.AddSelfBuff, not AddPartyBuffAsSelf.
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

    /// <summary>CAST step -- action sort 64, reachable via op15 or op16; arms the marker unconditionally.</summary>
    private static ActionInfo PartyBuffCastAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return BaseAction(FormationSkillCatalog.PartyBuffArmActionSort, skillNumber, gradeNum1, gradeNum2);
    }

    /// <summary>DONE step -- action sort 65, reachable via op15 only; advances Cast -&gt; Done.</summary>
    private static ActionInfo PartyBuffDoneAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return BaseAction(FormationSkillCatalog.PartyBuffConfirmActionSort, skillNumber, gradeNum1, gradeNum2);
    }

    /// <summary>
    ///     A plain rest/stand-up action (sort 0) -- records the same unconditional per-action skill/grade fields
    ///     <c>HandleMove</c> writes for every accepted action, WITHOUT ever touching <see cref="PartyBuffAction" />
    ///     (dispatches to <c>ApplyRestActionProtectionAndHeal</c> instead of the party-buff branch). Used to seed a
    ///     matching "previous action" for a CONFIRM's own staleness check while leaving the marker at its
    ///     untouched <see cref="PartyBuffAction.None" /> default -- a skill/grade pair reaching CONFIRM without
    ///     ever having gone through CAST/DONE at all.
    /// </summary>
    private static ActionInfo RestAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return BaseAction(0, skillNumber, gradeNum1, gradeNum2);
    }

    /// <summary>CONFIRM step -- op16 only, action sort 1; the wire-level "create effect value" trigger that actually writes the buff.</summary>
    private static ActionInfo SkillEffectConfirmAction(int skillNumber, int gradeNum1, int gradeNum2 = 0,
        int targetCharacterId = 0)
    {
        return BaseAction(1, skillNumber, gradeNum1, gradeNum2, targetCharacterId);
    }

    private static ActionInfo SkillCastStartAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return BaseAction(32, skillNumber, gradeNum1, gradeNum2);
    }

    /// <summary>Enters 4 allies into <paramref name="zone" /> and parties all of them with <paramref name="casterId" />, for an exactly-full 5-member party.</summary>
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
            Assert.True(partyRegistry.TryAnswer(allyId, true, out _, out _));
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

        // CAST (op15, Sort 64): arms the marker.
        zone.Post(ZoneCommand.Move(10, PartyBuffCastAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.Cast, caster!.PartyBuffAct);

        // DONE (op15, Sort 65): advances Cast -> Done.
        zone.Post(ZoneCommand.Move(10, PartyBuffDoneAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.Done, caster.PartyBuffAct);

        // CONFIRM (op16, Sort 1): both halves of the AND-gate now hold -> the buff is written.
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(FormationSkillId, GradeNum1, GradeNum2), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(15, caster.Buffs.Buff[FormationBuffSlot * 2]);
        Assert.Equal(50, caster.Buffs.Buff[FormationBuffSlot * 2 + 1]);
        // Single-use anti-replay consumption: a successful confirm resets the marker back to None.
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

        // CAST only -- the DONE (65) step is deliberately never sent, so the marker never advances past Cast.
        zone.Post(ZoneCommand.Move(10, PartyBuffCastAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.Cast, caster!.PartyBuffAct);

        // CONFIRM echoes the CAST action's own skill/grade (the last recorded action), so it passes the
        // staleness check -- but this is the NEW behavior the AND-combine fix introduces: before the fix,
        // HasFullPartyPresent alone (already satisfied here) would have let this succeed.
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(FormationSkillId, GradeNum1, GradeNum2), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, caster.Buffs.Buff[FormationBuffSlot * 2]);
        Assert.Equal(0, caster.Buffs.Buff[FormationBuffSlot * 2 + 1]);
        // The early return leaves the marker untouched -- still Cast, never reset (reset only happens after a
        // successful buff write).
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

        // Neither CAST nor DONE is ever sent -- a rest action (sort 0) instead records the same matching
        // skill/grade "previous action" fields a real CAST/DONE would, without ever touching the marker (see
        // RestAction's own remarks), so PartyBuffAct stays at its untouched None default.
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
        // No partyRegistry entries at all -- a solo caster's own PartyRegistry.GetMembers returns an empty list
        // (count 0 != PartyRegistry.MaxMembers), so HasFullPartyPresent is false regardless of the marker.
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (casterSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(casterSession, 1, "Caster")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var caster));

        // Drive the marker all the way to Done -- the marker side of the AND-gate is fully satisfied.
        zone.Post(ZoneCommand.Move(10, PartyBuffCastAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, PartyBuffDoneAction(FormationSkillId, GradeNum1, GradeNum2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(PartyBuffAction.Done, caster!.PartyBuffAct);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(FormationSkillId, GradeNum1, GradeNum2), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Pre-existing behavior, unaffected by the AND-combine fix: HasFullPartyPresent alone was always
        // sufficient to block this, independent of the marker.
        Assert.Equal(0, caster.Buffs.Buff[FormationBuffSlot * 2]);
    }

    [Fact]
    public void NonFormationSkill_WritesRegardlessOfPartyOrMarker()
    {
        // Skill 83 has RequiresFullParty == false and is never touched by the party-buff marker machinery at
        // all (it dispatches through the ordinary op15 category-2 mana-charge branch, not the CAST/DONE Sort
        // 64/65 branch) -- this AND-gate fix must be a complete no-op for it.
        var skillsById = new Dictionary<int, SkillDefinition> { [83] = CriticalUpSkill(10, 0, 12, 40) }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        // No party at all -- proves the gate genuinely never applies to this skill, not merely that a party
        // happened to be present.
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
        // Never armed, never reset -- this skill's confirm path never reads or writes PartyBuffAct at all.
        Assert.Equal(PartyBuffAction.None, caster.PartyBuffAct);
    }
}
