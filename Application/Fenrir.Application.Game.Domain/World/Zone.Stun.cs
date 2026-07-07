using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     <c>mCase</c> 5 (Stun, <see cref="ApplyStunAttack" />) and 6 (UnStun, <see cref="ApplyUnstunAttack" />)
///     resolution, plus the shared stun-pose broadcast/clear helpers <see cref="Simulation.StunCountdownSystem" />
///     also uses for natural expiry. Dispatched from <see cref="ApplyCombatCommand" /> (Zone.Combat.cs) --
///     everything else (kill hooks, PvP/PvM resolution) stays in that file; this one is stun's own home.
/// </summary>
public sealed partial class Zone
{
    /// <summary>"Stun" avatar-action pose (<c>mDATA.aAction.aSort==11</c>).</summary>
    private const int StunActionSort = 11;

    /// <summary>
    ///     Post-cure/expiry idle pose (<c>mDATA.aAction.aSort=1</c>, S07_MyGame04.cpp:449) -- NOT the same as the default
    ///     0 used elsewhere.
    /// </summary>
    private const int IdleActionSort = 1;

    /// <summary>
    ///     Buff slot 13 ("Stun Defense") -- replaces a stun attempt's computed block value with the flat
    ///     literal 100 (a finite, beatable mitigation, not unconditional immunity; see <see cref="StunResolver" />).
    /// </summary>
    private const int StunDefenseBuffSlot = 13;

    /// <summary>Buff slot 10 ("Critical") -- both sides of the team-stun kill-credit grant must hold this active.</summary>
    private const int CriticalBuffSlot = 10;

    /// <summary>Repeated-stun count at which the team-stun sub-mechanic force-kills the victim (S07_MyGame02.cpp:3727-3731).</summary>
    private const int TeamStunLockDeathThreshold = 10;

    /// <summary>
    ///     Reusable scratch buffer for <see cref="BroadcastIdleActionState" />'s neighbor-broadcast recipient
    ///     list -- see <see cref="_stunNeighborScratch" />'s own remarks for the reuse justification.
    /// </summary>
    private readonly List<int> _idleNeighborScratch = [];

    /// <summary>
    ///     Reusable scratch buffer for <see cref="BroadcastStunActionState" />'s neighbor-broadcast recipient
    ///     list -- same non-allocating shape and reuse justification as <c>Zone.PlayerLifecycle.cs</c>'s own
    ///     <c>_moveNeighborScratch</c>: single tick thread, cleared before use, consumed entirely by the
    ///     immediately-following broadcast call before <see cref="BroadcastStunActionState" /> returns. Kept
    ///     distinct from <see cref="_idleNeighborScratch" /> even though the two poses are mutually exclusive,
    ///     matching the rest of this codebase's one-scratch-field-per-call-site convention.
    /// </summary>
    private readonly List<int> _stunNeighborScratch = [];

    /// <summary>
    ///     ProcessAttack05 -- an avatar attempts to stun another avatar. Every failure path is a silent no-op;
    ///     see <see cref="StunResolver" />'s own remarks for what isn't modeled and why.
    /// </summary>
    private void ApplyStunAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;

        // Recorded onto the attacker's tracked location unconditionally, before any other check runs -- not
        // otherwise validated against the server-known position for this request (matches ATK_VAR2::create()).
        ApplySenderLocation(attackerState, command.AttackInfo);

        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;
        if (defenderState.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

        var usedSkillId = command.AttackInfo.AttackActionValue2;
        var usedSkillGradePoints = command.AttackInfo.AttackActionValue3;
        worldData.SkillsById.TryGetValue(usedSkillId, out var usedSkill);

        var stunResistSkillId = FindFirstLearnedSkill(defenderState, StunResolver.StunResistSkillIds,
            out var stunResistGrade);
        worldData.SkillsById.TryGetValue(stunResistSkillId, out var stunResistSkill);

        var request = new StunAttemptRequest(
            ToCombatantSnapshot(attackerState),
            ToCombatantSnapshot(defenderState),
            defenderState.ActionSort,
            defenderState.PshopOpen,
            ZonePvpZoneCatalog.AllowsEnemyTribeAttack(MapId),
            SharesActiveDuel(attackerState.CharacterId, defenderState.CharacterId),
            attackerState.VerifyEchoedActionState,
            usedSkillId,
            usedSkillGradePoints,
            attackerState.ActionSkillNumber,
            attackerState.ActionSkillGradeNum1 + attackerState.ActionSkillGradeNum2,
            usedSkill,
            stunResistSkillId != 0 ? stunResistSkill : null,
            stunResistGrade,
            defenderState.Buffs.Buff[StunDefenseBuffSlot * 2 + 1] > 0);

        var outcome = StunResolver.Resolve(request, _clock, _random);
        if (outcome.Rejected || !outcome.Success)
            return;

        defenderState.IsStunned = true;
        defenderState.StunDurationSeconds = outcome.ResolvedDurationSeconds;
        defenderState.StunCountdownAccumulatorTicks = 0;
        defenderState.CanUseConsumables = false;

        BroadcastStunActionState(defenderState, outcome.ResolvedDurationSeconds);

        if (outcome.IsTeamStunSkill)
            ApplyTeamStunSubMechanic(attackerState, defenderState);
    }

    /// <summary>
    ///     ProcessAttack06 -- an avatar attempts to cure/remove stun from another avatar. Every failure path is
    ///     a silent no-op; see <see cref="UnstunResolver" />'s own remarks for the two cited asymmetries versus
    ///     <see cref="ApplyStunAttack" />.
    /// </summary>
    private void ApplyUnstunAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var curerState))
            return;

        ApplySenderLocation(curerState, command.AttackInfo);

        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var targetState))
            return;
        if (targetState.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

        // CheckAttackPacket (S07_MyGame02.cpp:1718-1761), counting enabled -- see AttackPacketBudget's own
        // remarks. Silent reject, same as every other ProcessAttack06 rejection path.
        if (!AttackPacketBudget.TryConsume(curerState, command.AttackInfo.AttackActionValue4))
            return;

        var usedSkillId = command.AttackInfo.AttackActionValue2;
        var usedSkillGradePoints = command.AttackInfo.AttackActionValue3;
        worldData.SkillsById.TryGetValue(usedSkillId, out var usedSkill);

        var request = new UnstunAttemptRequest(
            ToCombatantSnapshot(curerState),
            ToCombatantSnapshot(targetState),
            targetState.IsStunned,
            targetState.PshopOpen,
            curerState.VerifyEchoedActionState,
            usedSkillId,
            usedSkillGradePoints,
            curerState.ActionSkillNumber,
            curerState.ActionSkillGradeNum1 + curerState.ActionSkillGradeNum2,
            usedSkill);

        var outcome = UnstunResolver.Resolve(request, _random);
        if (outcome.Rejected || !outcome.Success)
            return;

        ClearStun(targetState);
    }

    /// <summary>
    ///     Ends a stun early (a successful cure) or naturally (<see cref="Simulation.StunCountdownSystem" />'s
    ///     own expiry) -- both broadcast the same return-to-idle pose and reset the repeated-stun counter
    ///     (<c>S07_MyGame04.cpp:2593-2612</c>). A no-op if the character isn't currently stunned.
    /// </summary>
    public void ClearStun(PlayerRuntimeState state)
    {
        if (!state.IsStunned)
            return;

        state.IsStunned = false;
        state.StunDurationSeconds = 0;
        state.StunCountdownAccumulatorTicks = 0;
        state.CanUseConsumables = true;
        state.RepeatedStunCount = 0;

        BroadcastIdleActionState(state);
    }

    /// <summary>
    ///     Team-stun sub-mechanic (used skill id 80, <c>S07_MyGame02.cpp:3708-3725</c>): only runs when the
    ///     attacker's party has exactly <see cref="Social.Party.PartyRegistry.MaxMembers" /> members present and
    ///     connected in this zone instance -- a party short of five, or no party at all, aborts with no further
    ///     effect.
    /// </summary>
    private void ApplyTeamStunSubMechanic(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState)
    {
        var members = _partyRegistry.GetMembers(attackerState.CharacterId);
        if (members.Count != PartyRegistry.MaxMembers)
            return;

        List<PlayerRuntimeState>? present = null;
        foreach (var memberId in members)
            if (_players.TryGetValue(memberId, out var member))
                (present ??= []).Add(member);

        if (present is not { Count: 5 })
            return; // a member of the full 5-name roster isn't actually connected in this zone

        defenderState.RepeatedStunCount++;

        // Only the very first time the counter reaches 1 since its last reset attempts the kill-credit grant.
        if (defenderState.RepeatedStunCount == 1)
        {
            var defenderHasCriticalBuff = defenderState.Buffs.Buff[CriticalBuffSlot * 2 + 1] > 0;
            if (defenderHasCriticalBuff)
                foreach (var member in present)
                {
                    var memberHasCriticalBuff = member.Buffs.Buff[CriticalBuffSlot * 2 + 1] > 0;
                    if (!memberHasCriticalBuff)
                        continue;

                    if (!_killCooldownTracker.TryRegisterKill(member.CharacterId, defenderState.CharacterId,
                            DateTime.UtcNow))
                        continue;

                    member.MissionKillOtherTribe =
                        Math.Min(member.MissionKillOtherTribe + 1, KillCooldownTracker.MissionKillOtherTribeCap);
                    member.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                }
        }

        if (defenderState.RepeatedStunCount >= TeamStunLockDeathThreshold)
            // Distinct death entry point from ordinary HP-based death: no XP/loot/additional kill credit is
            // granted here (that all lives in the ordinary damage-based death path this bypasses entirely).
            ApplyDeath(defenderState.CharacterId, DeathCause.StunLock);
    }

    private static void ApplySenderLocation(PlayerRuntimeState state, AttackForProtocol attackInfo)
    {
        state.PosX = attackInfo.SenderLocation[0];
        state.PosY = attackInfo.SenderLocation[1];
        state.PosZ = attackInfo.SenderLocation[2];
    }

    private bool SharesActiveDuel(int attackerId, int defenderId)
    {
        return _duelRegistry.TryGetActiveDuel(attackerId, out var duel) && duel is not null &&
               duel.OpponentOf(attackerId) == defenderId;
    }

    /// <summary>
    ///     Slot-order scan of a character's learned skills for the first id in <paramref name="candidateIds" />;
    ///     0/default when none match.
    /// </summary>
    private static int FindFirstLearnedSkill(PlayerRuntimeState state,
        ImmutableArray<int> candidateIds, out int grade)
    {
        foreach (var slot in state.LearnedSkills.Keys.Order())
        {
            var learned = state.LearnedSkills[slot];
            if (!candidateIds.Contains(learned.SkillId))
                continue;

            grade = learned.Grade;
            return learned.SkillId;
        }

        grade = 0;
        return 0;
    }

    private void BroadcastStunActionState(PlayerRuntimeState state, int durationSeconds)
    {
        state.ActionSort = StunActionSort;
        state.ActionSkillNumber = 0;
        state.ActionSkillGradeNum1 = 0;
        state.ActionSkillGradeNum2 = 0;

        var action = BuildActionForPose(state, StunActionSort, durationSeconds);
        state.Session.Send(BuildAvatarActionRecv(state, action));

        // Uses _stunNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToArray().
        _stunNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_stunNeighborScratch, state.CurrentCell, state.CharacterId);
        BroadcastAvatarAction(_stunNeighborScratch, state, action);
    }

    private void BroadcastIdleActionState(PlayerRuntimeState state)
    {
        state.ActionSort = IdleActionSort;

        var action = BuildActionForPose(state, IdleActionSort, 0);
        state.Session.Send(BuildAvatarActionRecv(state, action));

        // Uses _idleNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToArray().
        _idleNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_idleNeighborScratch, state.CurrentCell, state.CharacterId);
        BroadcastAvatarAction(_idleNeighborScratch, state, action);
    }

    private static ActionInfo BuildActionForPose(PlayerRuntimeState state, int sort, int skillValue)
    {
        var pet = PetActionFieldsOf(state);

        return new ActionInfo
        {
            Type = 0,
            Sort = sort,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = pet.PetLocation,
            PetTargetLocation = pet.PetTargetLocation,
            PetFront = pet.PetFront,
            PetSort = pet.PetSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = skillValue
        };
    }
}
