using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Once-per-real-minute countdown for the per-minute timed-buff / scroll / experience-boost timers, plus
///     the paid-zone occupancy countdown-and-eviction, that the per-avatar zone tick runs under its per-minute
///     cadence gate (behavior contract "Timed-buff / scroll countdown + paid-zone occupancy eviction",
///     Server/ts25zone/S07_MyGame04.cpp:889-1133). Each surviving decrement fire-and-forget change-info
///     broadcasts the timer's new value to the owning session; a non-privileged occupant whose applicable
///     paid-zone counter is already 0 on a per-minute boundary is flagged for eviction via
///     <see cref="PlayerRuntimeState.PaidZoneEvictionPending" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:889-891 (the shared once-per-minute cadence gate, the same
///     one <see cref="PlayTimeAccrualSystem" />/<see cref="PetExpBoostCountdownSystem" />/
///     <see cref="SupportSkillTimeUpRatioMaintenanceSystem" /> fall under) ; :913-970 (group A, non-war zones) ;
///     :972-1025 (group B, war/RvR zones) ; :1046-1133 (paid-zone block, gated on <c>uUserSort &lt; 1</c>).
///     <para>
///         <b>Scope split with sibling systems.</b> This system deliberately does NOT touch the timers already
///         owned elsewhere so nothing is decremented twice: <c>aPetExpX2Time</c> (group-A S044) is
///         <see cref="PetExpBoostCountdownSystem" />'s; <c>aBuffX2Time</c> (group-C S042) and the
///         <c>aPremium</c> clear/ratio-recompute are <see cref="SupportSkillTimeUpRatioMaintenanceSystem" />'s;
///         zones 234-240 (<c>__GOD__</c> S033-S039) are <see cref="HoisundoCountdownSystem" />'s. The
///         active-effect / buff-view slot countdown (side effect 1 of the contract, per-second) is
///         <see cref="BuffExpirySystem" />'s. What remains -- and is implemented here -- is: group-A
///         <c>aDropItemTime</c>/<c>aFightingGodForDestroy</c>/<c>aDoubleExpTime1</c>/<c>aDoubleExpTime2</c>,
///         group-B <c>aAnimalDoubleExp</c>/<c>aDmgBoost</c>/<c>aHPBoost</c>/<c>aCriBoost</c>/<c>aWarriorPill</c>/
///         <c>aWarriorScroll</c>, and paid-zones 101/125/126/52.
///     </para>
///     <para>
///         <b>Broadcast sub-codes</b> are the numeric prefix of each timer's own <c>SNNN...</c> stat-sort name
///         (S027LUCKY_DROP -> 27, S018ZONE_101_TIME -> 18, ...), the same <c>SNNN</c> naming convention
///         <see cref="HoisundoCountdownSystem" /> already relies on for S033-S039 (verified there against
///         Server/Header/Protocol/STRUCT.h). Emitted self-only via <see cref="AvatarStatUpdateResponse" />
///         (Sort/Value/Value2), exactly as <see cref="HoisundoCountdownSystem" /> emits its own.
///     </para>
///     <para>
///         <b>Server-partition gate.</b> Group A applies on any map whose id is not in the excluded set
///         {1,6,11,140,38,37,119,124} and not one of the war-zone type flags {49,51,53,194,195,267}; group B
///         applies on {38,2,3,4,7,8,9,12,13,14,141,142,143} or any of those same war-zone flags -- so the two
///         are near-inverses (map 38 is excluded from A yet included in B, the contract's own worked example).
///         The legacy value is the process's numeric <c>mServerNumber</c>; this codebase already treats a
///         zone's <see cref="Zone.MapId" /> as that legacy zone/server number one-to-one, the same assumption
///         <see cref="HoisundoCountdownSystem" />'s own remarks document. Note: <see cref="PetExpBoostCountdownSystem" />
///         (group A) and <see cref="SupportSkillTimeUpRatioMaintenanceSystem" /> (group C) do NOT yet apply this
///         partition to their own timers -- a pre-existing, separately-owned gap this system does not reach into.
///     </para>
///     <para>
///         <b>Full-catch-up (integer-division) cadence</b>, mirroring <see cref="PetExpBoostCountdownSystem" />/
///         <see cref="SupportSkillTimeUpRatioMaintenanceSystem" />: a burst of N accumulated legacy ticks (a
///         stalled host catching up) ages every timer down by N real minutes in one pass. For the paid-zone
///         eviction this preserves the contract's "eviction is one minute late" shape for the production case
///         (counters start at 0, so the very first per-minute boundary finds the counter already 0 and flags
///         eviction): a counter found strictly positive is decremented (possibly to 0) and broadcast this pass
///         and only flags eviction on the following per-minute boundary. The one imperfection versus N literal
///         sequential legacy passes -- a counter of C with N &gt; C elapsing at once decrements straight to 0
///         without also flagging eviction that same pass -- is a double-rare (positive-counter + stalled-host)
///         case with no production reach while no grant path exists.
///     </para>
///     <para>
///         <b>Eviction is a flag, not an <c>Abort</c> here.</b> Per this workstream's charter the system stays
///         self-contained and hands eviction off through <see cref="PlayerRuntimeState.PaidZoneEvictionPending" />
///         for the zone's own session-teardown stage to consume -- it neither posts a command nor tears the
///         session down itself. Not modeled here (owned by the premium step, currently
///         <see cref="SupportSkillTimeUpRatioMaintenanceSystem" />'s <c>aPremium</c> clear): the
///         S116ITEM_PREMIUM premium-cleared broadcast and the special "premium just expired with
///         <c>aZone126Time</c> already 0" same-minute deferred return -- zone-126 here only reads whether
///         premium is currently active to gate its countdown.
///     </para>
/// </remarks>
public sealed class TimedBuffCountdownSystem : ISimulationSystem
{
    /// <summary>
    ///     MAX_MOUNT_EXP -- the mount double-experience countdown freezes once the mount reaches this accumulated
    ///     experience.
    /// </summary>
    private const int MaxMountExp = 100000;

    /// <summary>Group A runs when <see cref="Zone.MapId" /> is NOT in this set (server-id exclusions ∪ war-zone type flags).</summary>
    private static readonly FrozenSet<short> GroupAExcludedMaps =
        new short[] { 1, 6, 11, 140, 38, 37, 119, 124, 49, 51, 53, 194, 195, 267 }.ToFrozenSet();

    /// <summary>Group B runs when <see cref="Zone.MapId" /> IS in this set (server-id inclusions ∪ war-zone type flags).</summary>
    private static readonly FrozenSet<short> GroupBIncludedMaps =
        new short[] { 38, 2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143, 49, 51, 53, 194, 195, 267 }.ToFrozenSet();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var groupA = !GroupAExcludedMaps.Contains(zone.MapId);
        var groupB = GroupBIncludedMaps.Contains(zone.MapId);
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var state in zone.Players)
            TickPlayer(zone, state, legacyTicksElapsed, groupA, groupB, nowUnixSeconds);
    }

    private static void TickPlayer(Zone zone, PlayerRuntimeState state, int legacyTicksElapsed, bool groupA,
        bool groupB, long nowUnixSeconds)
    {
        // Same cross-shard-transfer-in-flight skip every other per-minute system in this cluster honors.
        if (state.IsMovingZone)
            return;

        state.TimedBuffCountdownAccrualTicks += legacyTicksElapsed;
        var minutesElapsed = state.TimedBuffCountdownAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.TimedBuffCountdownAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        if (groupA)
            TickGroupA(state, minutesElapsed);

        if (groupB)
            TickGroupB(state, minutesElapsed);

        TickPaidZones(zone, state, minutesElapsed, nowUnixSeconds);
    }

    private static void TickGroupA(PlayerRuntimeState state, int minutesElapsed)
    {
        state.DropItemTime = TickTimer(state, 27, state.DropItemTime, minutesElapsed);
        // aPetExpX2Time (S044) is decremented by PetExpBoostCountdownSystem, not here.
        state.FightingGodForDestroy = TickTimer(state, 20, state.FightingGodForDestroy, minutesElapsed);
        state.DoubleExpTime1 = TickTimer(state, 17, state.DoubleExpTime1, minutesElapsed);
        state.DoubleExpTime2 = TickTimer(state, 43, state.DoubleExpTime2, minutesElapsed);
    }

    private static void TickGroupB(PlayerRuntimeState state, int minutesElapsed)
    {
        // aAnimalDoubleExp decays only while a mount is active and its accumulated experience is below the cap;
        // either gate failing freezes it (Server/ts25zone/S07_MyGame04.cpp:972-1025).
        if (IsMountActiveBelowMaxExp(state))
            state.AnimalDoubleExp = TickTimer(state, 75, state.AnimalDoubleExp, minutesElapsed);

        state.DmgBoost = TickTimer(state, 46, state.DmgBoost, minutesElapsed);
        state.HPBoost = TickTimer(state, 47, state.HPBoost, minutesElapsed);
        state.CriBoost = TickTimer(state, 48, state.CriBoost, minutesElapsed);
        state.WarriorPill = TickTimer(state, 91, state.WarriorPill, minutesElapsed);
        state.WarriorScroll = TickTimer(state, 87, state.WarriorScroll, minutesElapsed);
    }

    private static void TickPaidZones(Zone zone, PlayerRuntimeState state, int minutesElapsed, long nowUnixSeconds)
    {
        // uUserSort < 1: the entire paid-zone countdown-and-eviction block is skipped for privileged users.
        if (state.UserSort >= 1)
            return;

        // A zone process hosts exactly one map, so at most one paid-zone branch applies per occupant.
        switch (zone.MapId)
        {
            // Zone 101 -- only while Level2 > 0 (a below-threshold character is neither charged nor evicted).
            case 101 when state.Level2 > 0:
                if (TickPaidZoneTimer(state, 18, state.Zone101Time, minutesElapsed, out var zone101))
                    state.PaidZoneEvictionPending = true;
                else
                    state.Zone101Time = zone101;
                break;

            // Zone 125 -- aZone125Time (TaiyanKeyTimer), no additional gate.
            case 125:
                if (TickPaidZoneTimer(state, 21, state.TaiyanKeyTimer, minutesElapsed, out var zone125))
                    state.PaidZoneEvictionPending = true;
                else
                    state.TaiyanKeyTimer = zone125;
                break;

            // Zone 126 with an active premium: premium suspends BOTH the countdown and the eviction entirely.
            case 126 when IsPremiumActive(state, nowUnixSeconds):
                break;

            // Zone 126 without an active premium.
            case 126:
                if (TickPaidZoneTimer(state, 22, state.Zone126Time, minutesElapsed, out var zone126))
                    state.PaidZoneEvictionPending = true;
                else
                    state.Zone126Time = zone126;
                break;

            // Server-52 aZone050Time2 -- adjacent, same eviction shape.
            case 52:
                if (TickPaidZoneTimer(state, 65, state.Zone050Time2, minutesElapsed, out var zone52))
                    state.PaidZoneEvictionPending = true;
                else
                    state.Zone050Time2 = zone52;
                break;
        }
    }

    /// <summary>
    ///     Paid-zone timer step: while the counter is above 0 it is decremented (full catch-up) and its new
    ///     value broadcast, returning false (no eviction this minute); once it is already 0 it returns true so
    ///     the caller flags eviction -- the contract's "decrement while above 0, evict on the following minute
    ///     when already 0" shape.
    /// </summary>
    private static bool TickPaidZoneTimer(PlayerRuntimeState state, int subCode, int current, int minutesElapsed,
        out int newValue)
    {
        if (current > 0)
        {
            newValue = Math.Max(0, current - minutesElapsed);
            Broadcast(state, subCode, newValue);
            return false;
        }

        newValue = 0;
        return true;
    }

    /// <summary>
    ///     Group-A/B timer step: decrements the counter (full catch-up) and broadcasts its new value, but only
    ///     while it is above 0 -- a counter already at or below 0 is untouched and silent (no broadcast).
    /// </summary>
    private static int TickTimer(PlayerRuntimeState state, int subCode, int current, int minutesElapsed)
    {
        if (current <= 0)
            return current;

        var next = Math.Max(0, current - minutesElapsed);
        Broadcast(state, subCode, next);
        return next;
    }

    private static bool IsMountActiveBelowMaxExp(PlayerRuntimeState state)
    {
        // aAnimalIndex 10-19 == actively mounted (garage slot + 10, the legacy offset encoding); the mount's
        // accumulated experience for that slot must still be below MAX_MOUNT_EXP.
        if (state.AnimalIndex is < 10 or > 19)
            return false;

        var slot = state.AnimalIndex - 10;
        return state.MountAccumulatedExp[slot] < MaxMountExp;
    }

    /// <summary>
    ///     Premium is "active" (and therefore suspends zone 126) while its expiry is at or after now -- the same
    ///     <c>&gt;=</c> convention <see cref="SupportSkillTimeUpRatioMaintenanceSystem" />'s own recompute uses.
    ///     A 0 (no premium) is never active, since <c>now</c> is a positive Unix timestamp.
    /// </summary>
    private static bool IsPremiumActive(PlayerRuntimeState state, long nowUnixSeconds)
    {
        return state.PremiumExpireUtc >= nowUnixSeconds;
    }

    private static void Broadcast(PlayerRuntimeState state, int sort, int value)
    {
        state.Session.Send(new AvatarStatUpdateResponse { Sort = sort, Value = value, Value2 = 0 });
    }
}
