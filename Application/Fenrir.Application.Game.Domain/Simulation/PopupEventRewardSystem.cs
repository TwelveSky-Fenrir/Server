using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Kill-streak popup-event reward system (<c>MyUtil::ProcessForKillOtherTribe</c>'s popup block +
///     <c>ProcessForPopUpReward</c>, <c>Server/ts25zone/S07_MyGame03.cpp:2602-3775</c>; the monster half at
///     <c>S07_MyGame05.cpp:2203-2254</c>). Two discrete kill events feed three per-character counters; when a
///     counter reaches its per-type threshold a reward fires (War Point +1, plus item draws -- see the gap note
///     below).
///     <para>
///         This is an <see cref="ISimulationSystem" />, but the counting is inherently event-driven (a kill has
///         no per-tick "flag" to scan for), so it has two external-trigger entry points --
///         <see cref="NotifyPvpKill" /> and <see cref="NotifyMonsterKill" /> -- called from the combat/monster
///         kill-resolution paths (both already on the owning zone's tick thread). Those triggers do the cheap,
///         kill-time-accurate part (per-type gating + counter advance, matching legacy's inline counting) and,
///         when a threshold is crossed, reset the counter and enqueue a reward-due marker. The heavier,
///         side-effecting reward delivery is drained and applied in <see cref="Simulate" /> on the same zone's
///         very next legacy tick -- keeping the combat hot path lean and isolating every reward fault to this
///         system's own per-system <c>try/catch</c> in <c>Zone.Simulate</c>, at a worst-case added latency of
///         one legacy tick (~500 ms), imperceptible against a kill-streak reward.
///     </para>
/// </summary>
/// <remarks>
///     <para>
///         DEFERRED (contract-first, no invented ids): <c>ProcessForPopUpReward</c>'s item-reward draws --
///         the primary base pool (six common items + per-type extras), the secondary previous-tribe rare-gear
///         tier (denominators 6000 Yanggok / 16000 Monster / guaranteed tier-1 for RegularWar-Ruins-Invasion),
///         and the RegularWar-only tertiary consumable/scroll band -- live at
///         <c>S07_MyGame03.cpp:3521-3775</c>, whose concrete item ids and rates are NOT in this system's source
///         behavior contract. They are intentionally NOT implemented here: the delivery MAPPING is known
///         (RegularWar primary item -> inventory-set; every other draw -> ground drop at the killer's position,
///         each with a cluster-wide elite-drop announcement, type 57 via center opcode 2000) but the item
///         tables themselves need a follow-up legacy-behavior-translator pass. See <see cref="DeliverReward" />.
///     </para>
///     <para>
///         NOT modeled, and why: the counter-update broadcasts (opcodes 401/402) and the elite-drop
///         announcement (type 57 / center opcode 2000) -- no such packet/broadcast helper exists on the Fenrir
///         side yet and packet authoring is out of this system's scope; the same-IP guards
///         (S07_MyGame03.cpp:2725-2731 Yanggok pre-count return; the RegularWar/Ruins/Invasion post-count check)
///         -- no session IP is exposed to the domain layer; the persisted avatar counter fields
///         (<c>aPopUpKillAvt/aPopUpKillMonster/aPopUpKillAvtWar</c>, persisted in legacy) -- counters here live
///         in-memory per <see cref="PlayerRuntimeState" /> instance and reset on zone (re)entry, since no
///         <c>game.Characters</c> column carries them yet, the same "not yet persisted" posture as
///         <see cref="PlayerRuntimeState.WarPoint" /> itself.
///     </para>
/// </remarks>
public sealed class PopupEventRewardSystem(PopupEventState state) : ISimulationSystem
{
    /// <summary>
    ///     One-directional PvP level-range gate (<c>S07_MyGame03.cpp:2660-2662, 2715-2718</c>): an attacker more
    ///     than this many COMBINED levels above the victim aborts the whole kill-reward routine before any popup
    ///     counting. Same constant/semantics as <c>Zone.CombinedLevelGapCap</c>, kept local so this self-contained
    ///     system re-applies the gate itself rather than trusting the trigger's caller.
    /// </summary>
    private const int CombinedLevelGapCap = 13;

    /// <summary>War Point increment on a reward (<c>S07_MyGame03.cpp:3546-3547</c>): +1, server-authoritative.</summary>
    private const int WarPointRewardAmount = 1;

    /// <summary>
    ///     Per-character popup counters, attached to the live <see cref="PlayerRuntimeState" /> instance rather
    ///     than stored in a keyed dictionary: the entry is reclaimed automatically when the player leaves the
    ///     zone (its <see cref="PlayerRuntimeState" /> becomes unreachable), so there is nothing to prune and no
    ///     leak, and no edit to <see cref="PlayerRuntimeState" /> is needed. Touched only from the owning zone's
    ///     tick thread (both trigger entry points and <see cref="Simulate" /> run there), and
    ///     <see cref="ConditionalWeakTable{TKey,TValue}" /> is itself thread-safe for the get-or-create.
    /// </summary>
    private readonly ConditionalWeakTable<PlayerRuntimeState, PopupCounters> _counters = new();

    /// <summary>
    ///     Per-map reward-due markers bridging a trigger's threshold crossing to that same map's next
    ///     <see cref="Simulate" /> drain. Keyed by <see cref="World.Zone.MapId" /> so each zone drains only its
    ///     own; effectively single-producer/single-consumer per map (both sides run on that zone's one tick
    ///     thread), with <see cref="ConcurrentQueue{T}" /> as a cheap, allocation-free-per-enqueue safety net.
    /// </summary>
    private readonly ConcurrentDictionary<short, ConcurrentQueue<PopupRewardDue>> _rewardDue = new();

    /// <summary>
    ///     Drains this zone's reward-due markers accumulated since the previous legacy tick and delivers each
    ///     reward. Self-contained: reads only the passed <paramref name="zone" /> and its own per-map queue; posts
    ///     no zone commands and mutates no cross-zone state.
    /// </summary>
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (!_rewardDue.TryGetValue(zone.MapId, out var queue))
            return;

        while (queue.TryDequeue(out var due))
            DeliverReward(zone, due);
    }

    /// <summary>
    ///     PvP-kill trigger (external): call from the resolved PvP kill-reward path
    ///     (<c>Zone.ApplyPvpKillRewards</c>, the Fenrir analog of <c>ProcessForKillOtherTribe</c>). Advances the
    ///     RegularWar/Ruins war counter or the Yanggok/Invasion PvP-avatar counter, gated by this shard's map
    ///     identity, the per-type on/off flag, the one-directional combined-level gate, and (Yanggok/Invasion)
    ///     the victim-at-level-cap requirement.
    /// </summary>
    public void NotifyPvpKill(Zone zone, PlayerRuntimeState killer, PlayerRuntimeState victim)
    {
        if (killer.CharacterId == victim.CharacterId)
            return;

        // Ready-state precondition (S07_MyGame03.cpp:2604-2611) is satisfied by construction: a
        // PlayerRuntimeState only exists in Zone._players once fully entered/"ready" (see Zone.HandleEnter).

        // One-directional level-range gate: only an attacker >13 combined levels ABOVE the victim is blocked.
        if (killer.CombinedLevel - victim.CombinedLevel > CombinedLevelGapCap)
            return;

        // Predicate is this shard's assigned map identity, never the victim's live cell (contract Preconditions).
        if (!PopupEventZoneCatalog.TryResolvePvpType(zone.MapId, out var type))
            return;

        if (!state.IsEnabled(type))
            return;

        // Yanggok/Invasion only count a victim who is exactly at the general level cap (LV_M33 = 145).
        // RegularWar/Ruins have no such gate. Yanggok's same-IP early return is a documented gap (no IP here).
        if (type is PopupEventType.YanggokPvp or PopupEventType.InvasionPvp &&
            victim.Level != LevelProgressionCalculator.MaxLevel)
            return;

        var counters = _counters.GetValue(killer, static _ => new PopupCounters());
        var threshold = PopupEventZoneCatalog.KillThreshold(type, zone.MapId);

        if (PopupEventZoneCatalog.UsesWarCounter(type))
        {
            counters.War = Math.Min(counters.War + 1, threshold);
            if (counters.War >= threshold)
            {
                counters.War = 0;
                Enqueue(zone.MapId, type, killer.CharacterId);
            }
        }
        else
        {
            counters.Avt = Math.Min(counters.Avt + 1, threshold);
            if (counters.Avt >= threshold)
            {
                counters.Avt = 0;
                Enqueue(zone.MapId, type, killer.CharacterId);
            }
        }
    }

    /// <summary>
    ///     Monster-kill trigger (external): call from the monster drop-resolution path
    ///     (<c>Monsters.MonsterSpawnScheduler.ProcessDeath</c>). <paramref name="dropEligible" /> is legacy's
    ///     pre-computed monster drop-eligibility flag (killer not &gt;9 levels above a normal monster, with the
    ///     martial-item/boss allowances -- <c>S07_MyGame05.cpp:2203-2234</c>); when false the monster popup
    ///     counter does not advance. Advances the monster counter (cap 400) and, on reaching 400, resets BOTH the
    ///     monster and PvP-avatar counters (<c>S07_MyGame05.cpp:2236-2251</c>).
    /// </summary>
    public void NotifyMonsterKill(Zone zone, PlayerRuntimeState killer, bool dropEligible)
    {
        if (!dropEligible)
            return;

        if (!PopupEventZoneCatalog.IsMonsterPopupMap(zone.MapId))
            return;

        if (!state.IsEnabled(PopupEventType.MonsterPve))
            return;

        var counters = _counters.GetValue(killer, static _ => new PopupCounters());
        counters.Monster = Math.Min(counters.Monster + 1, PopupEventZoneCatalog.MonsterKillThreshold);
        if (counters.Monster >= PopupEventZoneCatalog.MonsterKillThreshold)
        {
            counters.Monster = 0;
            counters.Avt = 0; // monster reward resets BOTH counters (S07_MyGame05.cpp:2249-2250)
            Enqueue(zone.MapId, PopupEventType.MonsterPve, killer.CharacterId);
        }
    }

    private void Enqueue(short mapId, PopupEventType type, int characterId)
    {
        var queue = _rewardDue.GetOrAdd(mapId, static _ => new ConcurrentQueue<PopupRewardDue>());
        queue.Enqueue(new PopupRewardDue(type, characterId));
    }

    /// <summary>
    ///     Applies one fired reward (<c>ProcessForPopUpReward</c>, <c>S07_MyGame03.cpp:3506-3775</c>). Only the
    ///     War Point +1 grant -- the one reward step whose magnitude the source contract fully specifies -- is
    ///     delivered here, through the server-authoritative, bounded <see cref="World.Zone.GrantWarPoints" />. The
    ///     item draws are deferred (see this class's own remarks): the delivery routing is documented inline so a
    ///     follow-up only has to supply the reward tables, without this file ever guessing an item id.
    /// </summary>
    private void DeliverReward(Zone zone, PopupRewardDue due)
    {
        // Re-check presence: the killer may have left/transferred between the trigger's enqueue and this drain
        // (same tick in the common case, but up to one legacy tick apart). GrantWarPoints also self-guards.
        if (!zone.TryGetPlayer(due.CharacterId, out _))
            return;

        // Side effect 3: War Point +1, broadcast to the killer's own client (Zone.GrantWarPoints handles the
        // AvatarStatUpdateResponse + dirty-mark).
        zone.GrantWarPoints(due.CharacterId, WarPointRewardAmount);

        // Side effects 4-6 (DEFERRED -- item ids/rates unrecoverable from the source contract; do NOT invent):
        //   4. Primary draw from the base pool (6 common + per-type extras, none for MONSTER_PVE):
        //        - due.Type == RegularWar  -> place into the killer's inventory (stack vs. fresh serial) + inventory-set update
        //        - otherwise               -> zone.SpawnGroundItem(itemId, 1, killer.PosX, killer.PosY, killer.PosZ, killer.Name, "", 0)
        //      then a cluster-wide elite-drop announcement (type 57 / center opcode 2000) on a successful grant.
        //   5. Secondary previous-tribe rare-gear tier (uses killer.PreviousTribe 0/1/2; denominators 6000
        //      Yanggok / 16000 Monster / guaranteed tier-1 for RegularWar/Ruins/Invasion) -- always ground-dropped.
        //   6. Tertiary consumable/scroll band (RegularWar ONLY) -- ground-dropped.
        // See S07_MyGame03.cpp:3521-3775 and this class's remarks; hand off to legacy-behavior-translator.
    }

    /// <summary>Three per-character popup kill counters (legacy avatar fields, STRUCT.h:555-557).</summary>
    private sealed class PopupCounters
    {
        /// <summary><c>aPopUpKillAvt</c> -- shared by Yanggok and Invasion popups.</summary>
        public int Avt;

        /// <summary><c>aPopUpKillMonster</c> -- Monster/PvE popup, hard-capped at 400.</summary>
        public int Monster;

        /// <summary><c>aPopUpKillAvtWar</c> -- shared by RegularWar and Ruins popups.</summary>
        public int War;
    }

    /// <summary>A fired reward awaiting delivery in the owning zone's next <see cref="Simulate" /> pass.</summary>
    private readonly record struct PopupRewardDue(PopupEventType Type, int CharacterId);
}
