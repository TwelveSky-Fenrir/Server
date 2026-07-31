using System.Buffers;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Core.Wire;
using Fenrir.Data.WriteBehind;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int DuelStateChangedSort = 7;

    private readonly List<int> _duelEndNeighborScratch = [];

    private readonly List<int> _duelStartNeighborScratch = [];

    private readonly Zone124MassDuelState _zone124MassDuel = new();

    private void ApplyDuelAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;

        ApplySenderLocation(attackerState, command.AttackInfo);

        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;

        if (!AttackPacketBudget.TryConsume(attackerState, command.AttackInfo.AttackActionValue4))
            return;

        var sharesActiveDuel = SharesActiveDuel(attackerState.CharacterId, defenderState.CharacterId);

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var defenderSnapshot = ToCombatantSnapshot(defenderState);

        var attackSkill = command.AttackInfo.AttackActionValue1 == 2 &&
                          worldData.SkillsById.TryGetValue(command.AttackInfo.AttackActionValue2,
                              out var skillDef)
            ? skillDef
            : null;

        var zone124OverrideActive = Zone124DuelOverrideResolver.IsActive(
            MapId == Zone124DuelOverrideResolver.Zone124MapId, _zone124MassDuel.RemainingUnits);

        var outcome = CombatResolver.ResolveDuelAttack(attackerSnapshot, defenderSnapshot, command.AttackInfo,
            _clock, attackSkill, _random, sharesActiveDuel, defenderState.PshopOpen, defenderState.ActionSort,
            zone124OverrideActive, attackerState.AttackBudgetEnforced, attackerState.ActionSkillNumber,
            attackerState.ActionSkillGradeNum1 + attackerState.ActionSkillGradeNum2);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] =
                0;

        var viewDamage = outcome.ViewDamage;
        var realDamage = outcome.DamageApplied;
        var reflectFired = false;
        if (outcome.Hit)
        {
            if (TryApplyReflectAndDestroyer(attackerState, defenderState, outcome, CrossAvatarAttackKind.Duel))
            {
                reflectFired = true;
                viewDamage = 0;
                realDamage = 0;
            }
            else
            {
                (viewDamage, realDamage) = ApplyHolyShieldAbsorption(defenderState, outcome);
            }
        }

        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = reflectFired ? 0 : outcome.ElementDamage,
                AttackViewDamageValue = viewDamage,
                AttackRealDamageValue = realDamage
            }
        };

        var recipients = CombatRecipients(attackerState, defenderState);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit || reflectFired)
            return;

        defenderState.Life -= realDamage;
        defenderState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (defenderState.Life <= 0)
            ApplyDeath(defenderState.CharacterId, DeathCause.Duel, (attackerState.PosX, attackerState.PosZ));
    }

    public void EndActiveDuel(PlayerRuntimeState state, DuelEndReason reason)
    {
        state.CanUseConsumables = true;
        state.Session.Send(new DuelEndResponse { Result = (int)reason });

        _duelEndNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_duelEndNeighborScratch, state.CurrentCell, state.CharacterId, state.PosX,
            state.PosY, state.PosZ);
        BroadcastAvatarAction(_duelEndNeighborScratch, state);
    }

    private void HandleBroadcastDuelStart(int requesterCharacterId, int opponentCharacterId,
        int duelUniqueNumber)
    {
        if (!_players.TryGetValue(requesterCharacterId, out var requester))
            return;

        BroadcastDuelStateChanged(requester, requesterCharacterId, requester.UniqueNumber,
            duelUniqueNumber, 1);

        if (_players.TryGetValue(opponentCharacterId, out var opponent) && !opponent.IsMovingZone)
            BroadcastDuelStateChanged(requester, opponentCharacterId, opponent.UniqueNumber,
                duelUniqueNumber, 2);
    }

    private void BroadcastDuelStateChanged(PlayerRuntimeState anchor, int subjectCharacterId,
        uint subjectUniqueNumber, int duelUniqueNumber, int roleMarker)
    {
        var response = new AvatarStateFlagResponse
        {
            ServerIndex = subjectCharacterId,
            UniqueNumber = subjectUniqueNumber,
            Sort = DuelStateChangedSort,
            Value01 = 1,
            Value02 = duelUniqueNumber,
            Value03 = roleMarker
        };

        var total = FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            _duelStartNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_duelStartNeighborScratch, anchor.CurrentCell, anchor.CharacterId,
                anchor.PosX, anchor.PosY, anchor.PosZ);
            foreach (var neighborId in _duelStartNeighborScratch)
                SendDuelStateChangedFrame(neighborId, span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void SendDuelStateChangedFrame(int recipientId, ReadOnlySpan<byte> frame)
    {
        try
        {
            if (TryGetBroadcastRecipient(recipientId, out _, out var clientSession))
                clientSession.SendRaw(frame);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} duel-state-changed broadcast to character {RecipientId} failed",
                MapId, recipientId);
        }
    }
}
