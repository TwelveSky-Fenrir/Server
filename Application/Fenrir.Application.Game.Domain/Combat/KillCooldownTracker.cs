using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Process-wide anti-farm gate for PvP kill rewards -- reimplements the legacy <c>KillServer::AddKill</c>
///     (<c>NEW_KILL_SERVER</c> branch, <c>Server/ts25playuser/S07_MyGame01.cpp:69-152</c>), which PLAYUSER used to
///     answer <c>ZPP_ZONE_CHECK_KILL_FOR_PLAYUSER_SEND</c> (opcode 43) before a PvP kill's reward was granted.
/// </summary>
/// <remarks>
///     Only the cooldown gate itself is reimplemented here -- see <see cref="Zone" />'s
///     <c>ApplyPvpKillRewards</c> for the full reward pipeline this now gates (CP formula/FFA-override,
///     hero-rank points, EXP, and the per-zone-gated <see cref="PlayerRuntimeState.MissionKillOtherTribe" />)
///     and <c>Fenrir.Application.Game.Domain.Combat.PvpKillRewardZoneCatalog</c>'s own remarks for what still
///     isn't modeled (item-drop payload, same-IP gating, the event-popup counter, the Regular-War-host CP
///     override, the guild-level counter).
///     <para>
///         Directional and per ordered pair, exactly like the legacy's nested attacker -&gt; defenser -&gt; last-kill-tick
///         map: repeatedly farming the same victim is gated, but the victim killing the farmer back is a separate
///         (reversed) pair and is never blocked by this.
///     </para>
///     <para>
///         Entries live for the process's lifetime -- the legacy never evicts old pairs either (only a full server
///         restart clears <c>mKillServer</c>), so unlike most registries in this codebase this one is deliberately
///         never pruned.
///     </para>
/// </remarks>
public sealed class KillCooldownTracker
{
    /// <summary>
    ///     10 legacy MAX_MISSION_KILL_OTHER_TRIBE_NUM cap -- <c>Server/Header/Protocol/DEFINE.h:401</c>. Also
    ///     the daily-mission claim threshold (<c>DailyMissionHandler.RequiredKillOtherTribe</c>): the legacy
    ///     reuses the same literal for both, so a claim always consumes exactly a full counter.
    /// </summary>
    public const int MissionKillOtherTribeCap = 10;

    /// <summary>
    ///     The literal <c>tKillDelay</c> passed to <c>U_ZONE_CHECK_KILL_FOR_PLAYUSER_SEND</c> at its one call site
    ///     (<c>Server/ts25zone/S07_MyGame03.cpp:2658</c>): 600,000 ms.
    /// </summary>
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<(int AttackerId, int DefenderId), DateTime> _lastRewardedKillUtc = new();

    /// <summary>
    ///     True the first time <paramref name="attackerId" /> kills <paramref name="defenderId" />, or once
    ///     <paramref name="cooldown" /> (<see cref="DefaultCooldown" /> when omitted) has elapsed since the last
    ///     time this exact ordered pair was granted -- and records <paramref name="utcNow" /> as that new grant
    ///     moment. False, with no state change, while still inside the window. Mirrors <c>AddKill</c>'s
    ///     <c>_success</c>/<c>_failed1</c> result exactly.
    /// </summary>
    public bool TryRegisterKill(int attackerId, int defenderId, DateTime utcNow, TimeSpan? cooldown = null)
    {
        var window = cooldown ?? DefaultCooldown;
        var key = (attackerId, defenderId);

        while (true)
        {
            if (!_lastRewardedKillUtc.TryGetValue(key, out var lastGrantedUtc))
            {
                if (_lastRewardedKillUtc.TryAdd(key, utcNow))
                    return true;

                continue; // another thread just inserted this pair for the first time -- re-read and retry
            }

            if (utcNow - lastGrantedUtc < window)
                return false;

            if (_lastRewardedKillUtc.TryUpdate(key, utcNow, lastGrantedUtc))
                return true;
            // another thread refreshed this pair first -- retry against its newer value
        }
    }
}
