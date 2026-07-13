using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int StunActionSort = 11;

    private const int IdleActionSort = 1;

    private const int StunDefenseBuffSlot = 13;

    private const int CriticalBuffSlot = 10;

    private const int TeamStunLockDeathThreshold = 10;

    private readonly List<int> _idleNeighborScratch = [];

    private readonly List<int> _stunNeighborScratch = [];

    private void ApplyStunAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;

        ApplySenderLocation(attackerState, command.AttackInfo);

        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;
        if (defenderState.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

        if (!AttackPacketBudget.TryConsume(attackerState, command.AttackInfo.AttackActionValue4, enforceCeiling: false))
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
            attackerState.AttackBudgetEnforced,
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
        defenderState.CanUseConsumables = false;

        BroadcastStunActionState(defenderState, outcome.ResolvedDurationSeconds);

        if (outcome.IsTeamStunSkill)
            ApplyTeamStunSubMechanic(attackerState, defenderState);
    }

    private void ApplyUnstunAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var curerState))
            return;

        ApplySenderLocation(curerState, command.AttackInfo);

        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var targetState))
            return;
        if (targetState.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

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
            curerState.AttackBudgetEnforced,
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

    public void ClearStun(PlayerRuntimeState state)
    {
        if (!state.IsStunned)
            return;

        state.IsStunned = false;
        state.StunDurationSeconds = 0;
        state.CanUseConsumables = true;
        state.RepeatedStunCount = 0;

        BroadcastIdleActionState(state);
    }

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
            return;

        defenderState.RepeatedStunCount++;

        if (defenderState.RepeatedStunCount == 1)
        {
            var defenderHasCriticalBuff = defenderState.Buffs.Buff[CriticalBuffSlot * 2 + 1] > 0;
            if (defenderHasCriticalBuff)
                foreach (var member in present)
                {
                    var memberHasCriticalBuff = member.Buffs.Buff[CriticalBuffSlot * 2 + 1] > 0;
                    if (!memberHasCriticalBuff)
                        continue;

                    ApplyPvpKillRewards(member, defenderState, true);
                }
        }

        if (defenderState.RepeatedStunCount >= TeamStunLockDeathThreshold)
            ApplyDeath(defenderState.CharacterId, DeathCause.StunLock);
    }

    private bool SharesActiveDuel(int attackerId, int defenderId)
    {
        return _duelRegistry.TryGetActiveDuel(attackerId, out var duel) && duel is not null &&
               duel.OpponentOf(attackerId) == defenderId;
    }

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
        state.ActionType = 0;
        state.ActionSkillNumber = 0;
        state.ActionSkillGradeNum1 = 0;
        state.ActionSkillGradeNum2 = 0;

        var action = BuildActionForPose(state, StunActionSort, durationSeconds);
        state.Session.Send(BuildAvatarActionRecv(state, action));

        _stunNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_stunNeighborScratch, state.CurrentCell, state.CharacterId, state.PosX,
            state.PosY, state.PosZ);
        BroadcastAvatarAction(_stunNeighborScratch, state, action);
    }

    private void BroadcastIdleActionState(PlayerRuntimeState state)
    {
        state.ActionSort = IdleActionSort;
        state.ActionType = ResolveEquippedWeaponActionType(state);

        var action = BuildActionForPose(state, IdleActionSort, 0);
        state.Session.Send(BuildAvatarActionRecv(state, action));

        _idleNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_idleNeighborScratch, state.CurrentCell, state.CharacterId, state.PosX,
            state.PosY, state.PosZ);
        BroadcastAvatarAction(_idleNeighborScratch, state, action);
    }

    private int ResolveEquippedWeaponActionType(PlayerRuntimeState state)
    {
        var equippedWeapon = state.Inventory.GetSlot(ContainerMatrix.Equipment, EquipmentSlots.WeaponSlot);
        var weaponSort = equippedWeapon is { } weapon &&
                         worldData.ItemsById.TryGetValue(weapon.ItemId, out var weaponDefinition)
            ? weaponDefinition.Item.Sort
            : (byte?)null;

        return WeaponClassResolver.GetActionType(weaponSort);
    }

    private static ActionInfo BuildActionForPose(PlayerRuntimeState state, int sort, int skillValue)
    {
        var pet = PetActionFieldsOf(state);

        return new ActionInfo
        {
            Type = state.ActionType,
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
