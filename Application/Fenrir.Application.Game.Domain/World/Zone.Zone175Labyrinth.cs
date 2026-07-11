using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Zone-side world helpers for the Zone175 "Labyrinth" 5-wave PvE mission. These are the alloc-free,
///     tick-thread-only primitives <see cref="Simulation.ZoneZone175MissionEffects" /> adapts to; the mission's
///     own state machine lives in <see cref="Simulation.Zone175MissionCore" /> and drives these through that
///     effects seam. Kept in their own partial so no existing <c>Zone.*.cs</c> file is touched.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:8609-9311 (mission lifecycle + reward routine),
///     Server/ts25zone/S07_MyGame02.cpp:2394-2416 (wave-boss special types 40-44).
/// </remarks>
public sealed partial class Zone
{
    /// <summary>
    ///     Interim terminal-kick disconnect reason. TODO(wire): add a dedicated <c>LabyrinthMissionEnded</c>
    ///     value to <see cref="DisconnectReason" /> (Network) so the disconnect-reason metric distinguishes this
    ///     from an unrelated timed-zone expiry -- see the workstream report. <see cref="DisconnectReason.TimedZoneExpired" />
    ///     (a timed zone instance ending for its occupants) is the least-wrong existing value in the meantime.
    /// </summary>
    private const DisconnectReason Zone175TerminalDisconnectReason = DisconnectReason.TimedZoneExpired;

    /// <summary>
    ///     Whether any player is present per the mission's presence rule (ready, not mid-zone-transition, not
    ///     hiding; a dead-but-present player still counts). Iterates <see cref="_players" /> directly rather than
    ///     <c>_players.Values</c> so no snapshot list is allocated -- this runs once per combat legacy tick.
    /// </summary>
    public bool HasAnyZone175QualifyingPlayer()
    {
        foreach (var (_, state) in _players)
            if (Zone175EligibilityRules.IsPresent(state))
                return true;

        return false;
    }

    /// <summary>
    ///     How many live monsters carry <paramref name="specialType" /> (a wave-boss special type, 40-44).
    ///     Alloc-free enumeration of <see cref="_monsters" />.
    /// </summary>
    public int CountLivingZone175WaveBosses(byte specialType)
    {
        var count = 0;
        foreach (var (_, monster) in _monsters)
            if (monster.Template.SpecialType == specialType)
                count++;

        return count;
    }

    /// <summary>
    ///     Removes every live wave-boss monster (special type 40-44) from the zone -- the "all mission monsters
    ///     are removed" step on wave clear/abort. NOTE: the fixed-cadence trickle monsters cannot be identified
    ///     for removal here because their concrete monster ids are an unrecovered GAP (see
    ///     <see cref="Simulation.ZoneZone175MissionEffects.SummonTrickle" />); only the wave bosses, keyed by
    ///     their cited special-type range, are removable today.
    /// </summary>
    public void RemoveZone175MissionMonsters()
    {
        foreach (var (index, monster) in _monsters)
            if (Zone175RewardTables.IsWaveBossSpecialType(monster.Template.SpecialType) &&
                _monsters.TryRemove(index, out _))
                RemoveMonsterFromGrid(monster);
    }

    /// <summary>
    ///     The wave-clear reward routine (<c>S07_MyGame01.cpp:8609-8745</c>) over every reward-eligible player
    ///     (present AND not dead): (a) clear stun, (b) experience = per-tier table x
    ///     <paramref name="experienceRatio" /> (currently 0 -- table is a GAP), (c) the fixed 100M/200M money
    ///     table, (d) drops (GAP -- unknown item ids, skipped), (e) the fixed per-stage CP, (f) boss-damage reset.
    ///     Iterates <see cref="_players" /> directly (alloc-free).
    /// </summary>
    public void GrantZone175WaveReward(int stage, float experienceRatio)
    {
        var money = Math.Min(Zone175RewardTables.MoneyForStage(stage), StoreMoneyPolicy.MaxMoney);
        var contributionPoints = Zone175RewardTables.ContributionPointsForStage(stage);

        foreach (var (_, state) in _players)
        {
            if (!Zone175EligibilityRules.IsRewardEligible(state))
                continue;

            // (a) any stun is cleared (S07_MyGame01.cpp:8637). Minimal per the cited "any stun is cleared":
            // the stun flag and its remaining duration only -- the separate team-stun repeated-stun counter is
            // not touched (not covered by this citation).
            if (state.IsStunned)
            {
                state.IsStunned = false;
                state.StunDurationSeconds = 0;
            }

            // (b) experience: rebirth-tier table x ratio, applied only if positive. GAP: the base table is
            // unrecovered, so WaveClearExperience returns 0 today and this branch is a no-op until it is filled.
            var experience = Zone175RewardTables.WaveClearExperience(state.RebirthCount, experienceRatio);
            if (experience > 0)
            {
                state.Experience += experience;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            }

            // (c) money: the fixed per-stage amount, server-authoritative (never client-derived), routed through
            // the same bounded write-behind grant a monster kill uses. DEFERRED: the legacy's per-player
            // over-money-cap silent skip (S07_MyGame01.cpp:8668-8672) cannot be evaluated here -- Fenrir keeps no
            // in-memory current-money balance on PlayerRuntimeState, so the grant is clamped to the absolute
            // StoreMoneyPolicy.MaxMoney ceiling instead, and the exact "skip if it would push this player over
            // the cap" needs a current-money field or a DB-side clamp (flagged for fenrir-database-engineer).
            if (money > 0)
                QueueMoneyGrant(state.CharacterId, money);

            // (d) drops: GAP -- the fixed per-stage item-drop lists are unrecovered item ids; not invented.

            // (e) the fixed per-stage CP award (S07_MyGame01.cpp:8734-8741).
            if (contributionPoints != 0)
                GrantContributionPoints(state.CharacterId, contributionPoints);

            // (f) reset this player's boss-damage accumulator (S07_MyGame01.cpp:8743).
            state.Zone175BossDamage = 0;
        }
    }

    /// <summary>
    ///     Force-disconnects every player currently in the zone -- the terminal-state kick. Collects sessions
    ///     first, then aborts (never aborting mid-enumeration of <see cref="_players" />), the same deferred-list
    ///     shape <c>DeathGateTickSystem</c>/<c>HoisundoCountdownSystem</c>/<c>ValleyWarSystem</c> use.
    /// </summary>
    public void ForceDisconnectAllForZone175()
    {
        List<ClientSession>? toKick = null;
        foreach (var (_, state) in _players)
            if (state.Session is ClientSession client)
                (toKick ??= []).Add(client);

        if (toKick is null)
            return;

        foreach (var client in toKick)
            client.Abort(Zone175TerminalDisconnectReason);
    }
}
