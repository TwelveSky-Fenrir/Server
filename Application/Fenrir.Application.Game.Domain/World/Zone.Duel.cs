using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Duel combat (<c>mCase</c> 1, <see cref="ApplyDuelAttack" />, dispatched from <see cref="ApplyCombatCommand" />
///     in Zone.Combat.cs) and the per-participant half of ending an active duel -- called by
///     <see cref="Simulation.DuelMaintenanceSystem" /> once it has resolved which end condition fired; the
///     registry-side half (removing both participants' <see cref="Social.Duel.DuelRegistry" /> entries) is that
///     system's own responsibility via <see cref="Social.Duel.DuelRegistry.TryEndActiveDuel" />.
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     Reusable scratch buffer for <see cref="EndActiveDuel" />'s neighbor-broadcast recipient list -- same
    ///     non-allocating shape and reuse justification as <c>Zone.PlayerLifecycle.cs</c>'s own
    ///     <c>_moveNeighborScratch</c>: single tick thread, cleared before use, consumed entirely by the
    ///     immediately-following broadcast call before <see cref="EndActiveDuel" /> returns.
    /// </summary>
    private readonly List<int> _duelEndNeighborScratch = [];

    /// <summary>
    ///     ProcessAttack01 -- an avatar attacks its matched duel opponent. Shares <c>AttackPlayer</c>'s core
    ///     damage resolution with <c>mCase</c> 2 (<see cref="CombatResolver.ResolveDuelAttack" />), but gated by
    ///     the duel-specific authorization check (<see cref="SharesActiveDuel" />, already used identically for
    ///     the stun-duel-exception gate in Zone.Stun.cs) instead of the zone-wide open-PvP/same-tribe gate
    ///     <c>mCase</c> 2 uses -- plus the defender's shop-open/attackable-action-state preconditions
    ///     (<see cref="CombatResolver.ResolveDuelAttack" />'s own <c>defenderPshopOpen</c>/<c>defenderActionSort</c>
    ///     parameters), part of <c>AttackPlayer</c>'s shared precondition block (S07_MyGame02.cpp:901-933) that
    ///     <c>mCase</c> 2 (<see cref="ApplyCombatCommand" />) also reproduces, via
    ///     <see cref="CombatResolver.ResolveEnemyTribeAttack" />'s identically-named parameters.
    ///     A qualifying kill routes to <see cref="ApplyDeath" /> with
    ///     <see cref="DeathCause.Duel" /> (armed for exactly this purpose -- see that enum's own remarks) rather
    ///     than <see cref="ApplyPvpKillRewards" />: whether the legacy's shared kill-reward pipeline (CP/EXP/
    ///     hero-points/mission-kill-counter) fires identically for a duel kill was not independently re-verified
    ///     by this behavior's own source contract, so Fenrir does not award any of it for a duel kill until that
    ///     is settled. <see cref="Simulation.DuelMaintenanceSystem" /> already watches every participant's
    ///     <see cref="PlayerRuntimeState.IsDead" /> flag every tick and ends the duel (win/loss) the moment it
    ///     flips -- this method only needs to get the defender to that state; it does not itself decide the
    ///     duel's outcome.
    /// </summary>
    /// <remarks>
    ///     NOT implemented: the zone124 unconditional x3-damage/forced-crit override in the final 10s of a duel
    ///     countdown (S07_MyGame02.cpp:1146-1150). Map 124 duels are already refused outright at
    ///     CZ_DUEL_ASK_SEND (<see cref="Simulation.DuelMaintenanceSystem" />'s own
    ///     <c>ScriptedDuelArenaMapId</c> guard), so no <see cref="Social.Duel.ActiveDuel" /> can ever exist there
    ///     today -- and the override's own "remaining time" counter identity/owner (not the same counter as
    ///     <see cref="Social.Duel.ActiveDuel.RemainingTicks" />) was left an open question by the source
    ///     contract this method was authored from, rather than guessed at here.
    /// </remarks>
    private void ApplyDuelAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;

        // Recorded onto the attacker's tracked location unconditionally, before any other check runs -- see
        // ApplySenderLocation's own remarks (Zone.Combat.cs). Must happen before this same packet's own
        // distance check, which CombatResolver.ResolveDuelAttack runs against attackerSnapshot below.
        ApplySenderLocation(attackerState, command.AttackInfo);

        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;

        // CheckAttackPacket (S07_MyGame02.cpp:1718-1761), counting enabled -- shared verbatim by ProcessAttack01
        // and ProcessAttack02 (AttackPacketBudget's own remarks list ProcessAttack01 as a counting-enabled
        // caller). Silent reject, matching every other rejection path in this method.
        if (!AttackPacketBudget.TryConsume(attackerState, command.AttackInfo.AttackActionValue4))
            return;

        // Duel-specific authorization gate (S07_MyGame02.cpp:935-943) -- duel attacks never evaluate the
        // zone-wide open-PvP/same-tribe gate mCase 2 uses instead.
        var sharesActiveDuel = SharesActiveDuel(attackerState.CharacterId, defenderState.CharacterId);

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var defenderSnapshot = ToCombatantSnapshot(defenderState);

        var attackSkill = command.AttackInfo.AttackActionValue1 == 2 &&
                          worldData.SkillsById.TryGetValue(command.AttackInfo.AttackActionValue2,
                              out var skillDef)
            ? skillDef
            : null;

        var outcome = CombatResolver.ResolveDuelAttack(attackerSnapshot, defenderSnapshot, command.AttackInfo,
            _clock, attackSkill, _random, sharesActiveDuel, defenderState.PshopOpen, defenderState.ActionSort);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] =
                0; // charge buff slot 8, value half -- single-use, same convention as mCase 2

        // "1 + attacker's weapon ItemId" on a hit (client picks the swing animation/effect from this), 0 on a miss.
        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = CombatRecipients(attackerState, defenderState);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        defenderState.Life -= outcome.DamageApplied;
        defenderState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (defenderState.Life <= 0)
            ApplyDeath(defenderState.CharacterId, DeathCause.Duel);
    }

    /// <summary>
    ///     RESET_DUEL (Server/ts25zone/S07_MyGame04.cpp:606-611): unconditionally lifts <paramref name="state" />'s
    ///     potion restriction, sends ZC_DUEL_END_RECV with the resolved <paramref name="reason" />, and
    ///     refreshes this avatar's state to nearby observers only -- unlike <see cref="GrantReviveEligibility" />,
    ///     this never self-echoes to <paramref name="state" />'s own session, since ZC_DUEL_END_RECV is already
    ///     that participant's own "this happened to you" signal.
    /// </summary>
    public void EndActiveDuel(PlayerRuntimeState state, DuelEndReason reason)
    {
        state.CanUseConsumables = true;
        state.Session.Send(new DuelEndResponse { Result = (int)reason });

        // Uses _duelEndNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToArray().
        _duelEndNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_duelEndNeighborScratch, state.CurrentCell, state.CharacterId, state.PosX,
            state.PosY, state.PosZ);
        BroadcastAvatarAction(_duelEndNeighborScratch, state);
    }
}
