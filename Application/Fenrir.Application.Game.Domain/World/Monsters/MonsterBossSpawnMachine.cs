using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Effects seam for <see cref="MonsterBossSpawnMachine" />'s Reload state, so the pure state-advance logic is
///     unit-testable without a live <see cref="Zone" />. The production implementation
///     (<see cref="MonsterBossSpawnSystem" />'s <c>ZoneBossSpawnSink</c>) resolves the monster id against the
///     world reference table, checks live occupancy of a reserved boss slot, and spawns a boss into a free slot.
/// </summary>
public interface IMonsterBossSpawnSink
{
    /// <summary>
    ///     Resolves <paramref name="monsterId" /> against the monster reference table, caching the resolved
    ///     template for the immediately-following <see cref="SpawnBoss" /> calls. Returns false when the id is
    ///     absent -- Reload then returns WITHOUT advancing state or spawning (a one-invocation stall-and-retry,
    ///     <c>Server/ts25zone/S10_MySummon.cpp:1087-1091</c>).
    /// </summary>
    public bool TryResolveCandidate(int monsterId);

    /// <summary>True when reserved boss slot <paramref name="serverIndex" /> currently holds no live boss (free to fill).</summary>
    public bool IsSlotFree(int serverIndex);

    /// <summary>
    ///     Creates one boss at <paramref name="serverIndex" /> from the last-resolved template, placed at
    ///     <paramref name="candidate" />'s stored coordinate verbatim (no dispersion, no altitude re-clamp).
    /// </summary>
    public void SpawnBoss(int serverIndex, in MonsterBossSummonCandidate candidate);
}

/// <summary>
///     Per-zone mutable state for the <c>SummonBossMonster</c> machine -- one instance per hosted map, mutated
///     only by that zone's own tick thread (single-writer invariant), the same posture as
///     <see cref="MonsterZoneSpawnState" />. Legacy modelled this as a process-wide singleton shared by the whole
///     zone process (<c>Server/ts25zone/H10_MySummon.h:20-23</c>); Fenrir hosts many maps per process, so it
///     becomes one of these per zone.
/// </summary>
public sealed class MonsterBossSpawnZoneState
{
    /// <summary>This zone's boss-candidate pool (already odd-count-normalized). A count below 1 makes the machine inert.</summary>
    public required ImmutableArray<MonsterBossSummonCandidate> Candidates { get; init; }

    /// <summary>
    ///     Base index of this zone's contiguous 20-slot reserved boss-object window (see
    ///     <see cref="MonsterBossSpawnMachine.DefaultBossSlotBase" />).
    /// </summary>
    public required int SlotBase { get; init; }

    /// <summary>
    ///     Per-zone RNG for the random candidate pick each Reload -- never shared across zones (System.Random is not
    ///     thread-safe).
    /// </summary>
    public required Random Random { get; init; }

    /// <summary>
    ///     The effects seam (resolve id / slot occupancy / spawn) -- one reusable instance per zone, never per
    ///     invocation.
    /// </summary>
    public required IMonsterBossSpawnSink Sink { get; init; }

    /// <summary>The current live state; the subsystem starts in <see cref="MonsterBossSummonState.Reload" />.</summary>
    public MonsterBossSummonState State { get; set; } = MonsterBossSummonState.Reload;

    /// <summary>
    ///     Monotonic legacy-tick counter (never reset). The 3-hour Wait hold is measured against this, so a
    ///     stalled host that catches up many ticks at once still never shortens the hold.
    /// </summary>
    public long ElapsedLegacyTicks { get; set; }

    /// <summary>Legacy ticks accrued toward the next 20-tick invocation boundary; reset to 0 each invocation.</summary>
    public int TicksSinceLastInvocation { get; set; }

    /// <summary>
    ///     <see cref="ElapsedLegacyTicks" /> captured when the timer anchor was last written (end of Reload, then
    ///     overwritten end of Check). Only the Check write is ever read: the Wait state measures its 3-hour hold
    ///     against this (<c>Server/ts25zone/S10_MySummon.cpp:1113,1128,1131</c>).
    /// </summary>
    public long AnchorTick { get; set; }
}

/// <summary>
///     The pure, allocation-free core of the legacy <c>SummonBossMonster</c> 3-state boss-respawn machine
///     (Reload -&gt; Check -&gt; Wait-3h -&gt; Reload), <c>Server/ts25zone/S10_MySummon.cpp:1066-1216</c>. Advances
///     at most one state per 20-tick invocation and drives all world effects through
///     <see cref="IMonsterBossSpawnSink" /> so it can be unit-tested with a fake sink, no <see cref="Zone" />
///     required. <see cref="MonsterBossSpawnSystem" /> is the <see cref="Simulation.ISimulationSystem" /> adapter
///     that owns per-zone state and the production sink.
/// </summary>
public static class MonsterBossSpawnMachine
{
    /// <summary>
    ///     The "every 20th tick" invocation cadence: legacy calls the routine only when the running tick count is
    ///     an exact multiple of 20 (<c>Server/ts25zone/S07_MyGame01.cpp:2895-2905,3033-3041</c>), processing
    ///     exactly one state per crossing. Coincidentally the same 20 legacy ticks (~10 s) as
    ///     <see cref="Simulation.SimulationClock.MonsterRespawnScanLegacyTicks" />, but a semantically distinct
    ///     constant -- do not couple them.
    /// </summary>
    public const int InvocationIntervalLegacyTicks = 20;

    /// <summary>
    ///     3 hours expressed in 500 ms legacy ticks: 10,800,000 ms / 500 ms = 21,600 ticks. The exact Wait-state
    ///     hold (<c>Server/Header/datetime.h:28-34</c> yields 10,800,000 ms for 3 hours;
    ///     <c>S10_MySummon.cpp:1130-1136</c> is the hold).
    /// </summary>
    public const long WaitDurationLegacyTicks = 21_600;

    /// <summary>Hard cap of boss creations per Reload (<c>S10_MySummon.cpp:1096-1110</c>): stop after 5.</summary>
    public const int MaxSpawnsPerReload = 5;

    /// <summary>The reserved boss-slot window size scanned each Reload (<c>S10_MySummon.cpp:1094</c>): a hardcoded 20.</summary>
    public const int BossSlotWindowSize = 20;

    /// <summary>
    ///     Default base index of the contiguous 20-slot reserved boss window. Legacy resolves this per server at
    ///     boot from a chain of mutable global counters (default 3800 for an ordinary server,
    ///     <c>S01_MainApplication.cpp:40-52</c>; doubled to 7600 for the "dungeon" set,
    ///     <c>S02_MyServer.cpp:143-168</c>; reset to 0 for instance/FFA types, <c>:190-205</c>). Fenrir does not
    ///     model the per-server variation (no fixed-size shared-memory object pool exists here to resize), the
    ///     same posture <see cref="MonsterSpawnScheduler" />'s regular-table capacity takes toward per-shard
    ///     resizing. The flat default 3800 is deliberately disjoint from <see cref="MonsterSpawnScheduler" />'s
    ///     own <c>ServerIndex</c> range (1..3400, its <c>RegularMonsterTableCapacity</c>), so a boss slot's
    ///     <c>ServerIndex</c> can never collide with a regular-monster slot's inside <see cref="Zone" />'s monster
    ///     dictionary.
    /// </summary>
    public const int DefaultBossSlotBase = 3800;

    /// <summary>
    ///     Accrues <paramref name="legacyTicksElapsed" /> and, when a 20-tick invocation boundary is crossed,
    ///     processes exactly ONE state. Uses the same reset-to-0 one-invocation-per-call throttle as
    ///     <see cref="MonsterSpawnScheduler" />'s respawn scan rather than a catch-up loop: a stalled host that
    ///     jumps many ticks advances at most one state per Simulate call, the established Fenrir cadence choice.
    ///     The wait DURATION is still measured against the monotonic
    ///     <see cref="MonsterBossSpawnZoneState.ElapsedLegacyTicks" />,
    ///     so a stall never shortens the 3-hour hold; it only defers a state transition by at most one Simulate.
    /// </summary>
    public static void Advance(MonsterBossSpawnZoneState state, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed < 0)
            legacyTicksElapsed = 0; // a clock hiccup contributes nothing, never rewinds

        state.ElapsedLegacyTicks += legacyTicksElapsed;
        state.TicksSinceLastInvocation += legacyTicksElapsed;

        if (state.TicksSinceLastInvocation < InvocationIntervalLegacyTicks)
            return;

        state.TicksSinceLastInvocation = 0;
        ProcessOneInvocation(state);
    }

    private static void ProcessOneInvocation(MonsterBossSpawnZoneState state)
    {
        // Empty pool: every invocation returns immediately, the machine never spawns (precondition,
        // S10_MySummon.cpp:1068-1071). MonsterBossSpawnSystem already skips building state for empty pools, so
        // this is defensive.
        if (state.Candidates.Length < 1)
            return;

        switch (state.State)
        {
            case MonsterBossSummonState.Reload:
                ProcessReload(state);
                break;

            case MonsterBossSummonState.Check:
                // The historical liveness loop is commented out in the legacy source: the only live effect is to
                // advance to Wait and write the anchor the Wait state actually reads (S10_MySummon.cpp:1115-1129).
                state.State = MonsterBossSummonState.Wait;
                state.AnchorTick = state.ElapsedLegacyTicks;
                break;

            case MonsterBossSummonState.Wait:
                // Hold until 3 hours elapse from the Check anchor, then reset to Reload -- no spawn in this state
                // (S10_MySummon.cpp:1130-1136). At exactly 3 hours it resets (< holds, >= resets).
                if (state.ElapsedLegacyTicks - state.AnchorTick < WaitDurationLegacyTicks)
                    return;
                state.State = MonsterBossSummonState.Reload;
                break;

            default:
                // Codes 0/4 are dead in legacy and never assigned here; treat any unexpected value as Reload.
                state.State = MonsterBossSummonState.Reload;
                break;
        }
    }

    private static void ProcessReload(MonsterBossSpawnZoneState state)
    {
        // One random candidate, reduced modulo the pool size (S10_MySummon.cpp:1084-1086). Random.Next(count) is
        // the idiomatic unbiased equivalent; the legacy full-range-draw-modulo-poolsize introduces a slight
        // modulo bias toward lower indices, which Fenrir deliberately does NOT reproduce -- the bias is not a
        // gameplay-meaningful behavior and the underlying RNG identity is itself a flagged open question.
        var index = state.Random.Next(state.Candidates.Length);
        var candidate = state.Candidates[index];

        if (!state.Sink.TryResolveCandidate(candidate.MonsterId))
            // Unresolvable monster id: return WITHOUT advancing state and WITHOUT spawning. The next invocation
            // re-rolls a fresh random candidate -- a one-invocation stall-and-retry, not a permanent stall unless
            // every pool entry is unresolvable (S10_MySummon.cpp:1087-1091).
            return;

        var spawned = 0;
        for (var i = 0; i < BossSlotWindowSize && spawned < MaxSpawnsPerReload; i++)
        {
            var serverIndex = state.SlotBase + i;
            if (!state.Sink.IsSlotFree(serverIndex))
                continue; // an occupied slot is skipped and does NOT count toward the cap of 5

            // Every spawn in this Reload reuses the SAME selected candidate record: up to 5 identical-id bosses
            // are created stacked at one exact coordinate (verbatim, no dispersion) -- the actual behavior, not an
            // artifact (S10_MySummon.cpp:1094-1110,753-761).
            state.Sink.SpawnBoss(serverIndex, candidate);
            spawned++;
        }

        state.State = MonsterBossSummonState.Check;
        state.AnchorTick = state.ElapsedLegacyTicks; // overwritten by Check before Wait ever reads it
    }
}
