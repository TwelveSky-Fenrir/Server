using System.Linq;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     C16 popup-event kill-trigger forwarding -- the wiring <see cref="Simulation.PopupEventRewardSystem" /> was
///     built against but never actually reached from either kill-resolution path (deferred since the system's
///     own introduction; see that class's own remarks for what it does once notified). Both trigger methods
///     below are the ONLY call sites of <see cref="Simulation.PopupEventRewardSystem.NotifyPvpKill" />/
///     <see cref="Simulation.PopupEventRewardSystem.NotifyMonsterKill" /> anywhere in this codebase.
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     This zone's own <see cref="Simulation.PopupEventRewardSystem" /> instance, resolved once at
    ///     construction from the shared <c>simulationSystems</c> list DI already builds -- that system is
    ///     registered BOTH directly (<c>services.AddSingleton&lt;PopupEventRewardSystem&gt;()</c>) and as an
    ///     <see cref="ISimulationSystem" /> (so it still gets its own <see cref="Simulation.PopupEventRewardSystem.Simulate" />
    ///     call every tick, draining whichever reward-due markers this zone's own triggers queued), so the exact
    ///     same instance is reachable from this list without a second, independently-resolved dependency. Null
    ///     only when no such system is registered -- e.g. a test <see cref="Zone" /> built with a hand-rolled
    ///     <c>simulationSystems</c> list that omits it -- in which case both trigger methods below degrade to a
    ///     silent no-op, the same "optional cross-cutting dependency" posture every other one on this type
    ///     (<c>towerWar</c>, <c>worldState</c>, ...) already has.
    /// </summary>
    private readonly PopupEventRewardSystem? _popupEventRewardSystem =
        simulationSystems.OfType<PopupEventRewardSystem>().FirstOrDefault();

    /// <summary>
    ///     C16 PvP-kill popup-event trigger. Called from <see cref="ApplyPvpKillRewards" />, right after that
    ///     method's own <see cref="CombinedLevelGapCap" /> gate and BEFORE its <see cref="_killCooldownTracker" />
    ///     anti-farm check -- <see cref="Simulation.PopupEventRewardSystem" />'s own counting has no cooldown-gate
    ///     equivalent in the cited legacy source, and <see cref="_killCooldownTracker" /> is a Fenrir-only C05
    ///     addition with no legacy counterpart, so the popup counter must advance on every qualifying kill even
    ///     when the anti-farm cooldown independently withholds the CP/EXP/hero-point reward for a repeat kill of
    ///     the same victim.
    /// </summary>
    private void NotifyPopupEventPvpKill(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState)
    {
        _popupEventRewardSystem?.NotifyPvpKill(this, attackerState, defenderState);
    }

    /// <summary>
    ///     C16 monster-kill popup-event trigger. Called from <c>Monsters.MonsterSpawnScheduler.ProcessDeath</c>
    ///     with legacy's own pre-computed drop-eligibility flag (killer not more than 9 levels above a normal
    ///     monster, with the martial-item/boss exemption -- <see cref="Loot.MonsterDropRoller.IsEligible" />,
    ///     the same gate that already decides whether that same kill's generic loot pipeline runs at all).
    /// </summary>
    public void NotifyPopupEventMonsterKill(PlayerRuntimeState killer, bool dropEligible)
    {
        _popupEventRewardSystem?.NotifyMonsterKill(this, killer, dropEligible);
    }
}
