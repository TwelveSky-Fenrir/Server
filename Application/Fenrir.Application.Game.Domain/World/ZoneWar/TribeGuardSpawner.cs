using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Tuning knobs for <see cref="TribeGuardSpawner" /> that have no natural home on <see cref="GuardPostCatalog" />
///     itself (a per-process switch, not per-post data).
/// </summary>
public sealed class TribeGuardOptions
{
    /// <summary>
    ///     The legacy per-server-instance <c>mSERVER_INFO.mJonNangin</c> flag (<c>Server/Header/ini.h:328</c>,
    ///     <c>Server/Header/api.h:157</c>, consumed at <c>S10_MySummon.cpp:1802-1807</c>): when false, every
    ///     4th-tribe post (<see cref="GuardPostDefinition.TribeId" /> == 3) is skipped in its entirety on every
    ///     call, for both slot invalidation and spawning.
    /// </summary>
    public bool FourthTribeGuardPostsEnabled { get; init; } = true;
}

/// <summary>
///     Per-slot bookkeeping the tick thread owns. Counted in legacy ticks (decremented every real
///     <see cref="TribeGuardSpawner.Simulate" /> call, not just on a full-evaluation pass) rather than wall
///     clock, so it advances purely from <see cref="Zone.Tick" /> like every other respawn timer in this
///     codebase (<see cref="Monsters.MonsterSpawnScheduler" />'s own <c>RespawnTicksRemaining</c>) -- keeps
///     this class exactly as deterministically testable as its sibling.
/// </summary>
internal sealed class GuardSlotRuntimeState
{
    public bool Armed;
    public int RespawnTicksRemaining;
}

/// <summary>Everything <see cref="TribeGuardSpawner" /> needs to remember for one zone, tick-thread-owned.</summary>
internal sealed class GuardZoneState
{
    public readonly Dictionary<int, GuardSlotRuntimeState> Slots = new();
    public bool BootPassDone;
    public int ForceOrdinaryPending;
    public int ForceZone038WinnerPending;
    public int TicksSinceFullScan;
}

/// <summary>
///     <c>MySummon::SummonGuard(tCheckZone038, tCheckFirst)</c> (<c>Server/ts25zone/S10_MySummon.cpp:1323-1857</c>):
///     the tribe/zone038-winner capital-guard spawner. One instance is shared process-wide (registered as an
///     <see cref="ISimulationSystem" />, same posture as <see cref="Monsters.MonsterSpawnScheduler" />);
///     per-zone mutable bookkeeping lives in <see cref="_stateByZone" />.
///     <para>
///         The boot-time forced pass (<c>MyGame::Init</c>, <c>S07_MyGame01.cpp:1663-1665</c>) is modeled as
///         "run a forced pass on this zone's own very first <see cref="Simulate" /> call" -- the same
///         "InitialPopDone" idiom <see cref="Monsters.MonsterSpawnScheduler" /> already uses for its own
///         region pool -- rather than a separate boot hook, since every <see cref="Zone" /> starts ticking at
///         process boot regardless. The periodic every-20-tick re-evaluation
///         (<c>S07_MyGame01.cpp:2884-2889, 2900-2904, 3035-3041</c>) and the two forced-resummon triggers this
///         function has beyond boot (the HSB broadcast relay, and the zone038-battle-conclusion call) are
///         wired from <see cref="ZoneEventBroadcaster" /> via <see cref="ForceOrdinaryResummon" /> /
///         <see cref="ForceZone038WinnerResummon" />.
///     </para>
///     <para>
///         Not modeled: the neutral tribe-symbol attack-cooldown reset and any Combat-layer consumer of these
///         spawns -- out of this class's own World/ZoneWar scope. See the cluster's own final report.
///     </para>
/// </summary>
public sealed class TribeGuardSpawner(
    WorldDataCache worldData,
    GuardPostCatalog catalog,
    WorldStateService? worldState = null,
    TribeGuardOptions? options = null) : ISimulationSystem
{
    /// <summary>The only map the zone038-winner pool ever handles (<c>S10_MySummon.cpp:1747-1782</c>).</summary>
    public const short Zone038MapId = 38;

    /// <summary><c>(mTickCount % 20) == 0</c> -- the periodic re-evaluation cadence (<c>S07_MyGame01.cpp:2878</c> et al).</summary>
    private const int FullEvaluationCadenceLegacyTicks = 20;

    /// <summary>
    ///     No legacy leash-distance figure is given for a stationary guard post; a conservative fixed patrol
    ///     radius, same "data-driven stand-in" posture as <see cref="Monsters.MonsterAiSystem" />'s own
    ///     documented leash-radius gap.
    /// </summary>
    private const float GuardLeashRadius = 15f;

    private const int OrdinaryPoolServerIndexBase = 1_000_000;
    private const int Zone038WinnerPoolServerIndexBase = 1_001_000;

    /// <summary>
    ///     <c>tCheckZone038 == false</c> eligible map/server numbers -- every other map is an unconditional
    ///     no-op for the ordinary pool (<c>S10_MySummon.cpp:1339-1741, 1739-1740</c>).
    /// </summary>
    public static readonly IReadOnlySet<short> OrdinaryEligibleMapIds =
        new HashSet<short>([38, 2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143]);

    private readonly TribeGuardOptions _options = options ?? new TribeGuardOptions();
    private readonly ConcurrentDictionary<short, GuardZoneState> _stateByZone = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new GuardZoneState());

        // Every armed slot's cooldown counts down every real tick, not only on a full-evaluation pass --
        // matches MonsterSpawnScheduler's own RespawnTicksRemaining decrement cadence.
        foreach (var slotState in state.Slots.Values)
            if (slotState.RespawnTicksRemaining > 0)
                slotState.RespawnTicksRemaining = Math.Max(0, slotState.RespawnTicksRemaining - legacyTicksElapsed);

        if (!state.BootPassDone)
        {
            state.BootPassDone = true;
            EvaluateOrdinaryPool(zone, state, true);
            EvaluateZone038WinnerPool(zone, state, true);
        }

        state.TicksSinceFullScan += legacyTicksElapsed;
        var forceOrdinary = Interlocked.Exchange(ref state.ForceOrdinaryPending, 0) != 0;
        var forceZone038Winner = Interlocked.Exchange(ref state.ForceZone038WinnerPending, 0) != 0;

        var due = state.TicksSinceFullScan >= FullEvaluationCadenceLegacyTicks;
        if (!due && !forceOrdinary && !forceZone038Winner)
            return;

        state.TicksSinceFullScan = 0;
        EvaluateOrdinaryPool(zone, state, forceOrdinary);
        EvaluateZone038WinnerPool(zone, state, forceZone038Winner);
    }

    /// <summary>
    ///     Requests a forced ordinary-pool pass on <paramref name="zone" />'s own next <see cref="Simulate" />
    ///     call -- safe from any thread (broadcast-relay callers included).
    /// </summary>
    public void ForceOrdinaryResummon(Zone zone)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new GuardZoneState());
        Interlocked.Exchange(ref state.ForceOrdinaryPending, 1);
    }

    /// <summary>Same as <see cref="ForceOrdinaryResummon" />, for the zone038-winner pool.</summary>
    public void ForceZone038WinnerResummon(Zone zone)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new GuardZoneState());
        Interlocked.Exchange(ref state.ForceZone038WinnerPending, 1);
    }

    private void EvaluateOrdinaryPool(Zone zone, GuardZoneState state, bool forceFirstPass)
    {
        if (!OrdinaryEligibleMapIds.Contains(zone.MapId))
            return; // whole call is a no-op for this map

        // Side effect §1 (S10_MySummon.cpp:1809-1816): force-reset (tCheckFirst) wipes the whole normal-guard
        // region BEFORE the spawn loop, so every configured post is re-planted fresh (a damaged guard is reset
        // to full HP), not left untouched. The periodic 20-tick pass runs in cooldown mode and never wipes.
        if (forceFirstPass)
            TribeGuardForceResetSweep.Wipe(zone, OrdinaryPoolServerIndexBase);

        foreach (var post in catalog.OrdinaryPosts)
        {
            if (post.MapId != zone.MapId)
                continue;

            // The 4th-tribe config gate (mJonNangin) skips the WHOLE post, every call.
            if (post.TribeId == 3 && !_options.FourthTribeGuardPostsEnabled)
                continue;

            // Conditional 5th post (a tribe's own home-capital map): skipped whole while that tribe has lost
            // its own tribe symbol. No worldState wired -> conservatively treated as "not owned" -> skipped.
            if (post.RequiresTribeSymbolOwnedBy is { } ownerTribeId &&
                !(worldState is not null && worldState.GetTribe(ownerTribeId).HasSymbol))
                continue;

            EvaluatePost(zone, state, post, OrdinaryPoolServerIndexBase, forceFirstPass);
        }
    }

    private void EvaluateZone038WinnerPool(Zone zone, GuardZoneState state, bool forceFirstPass)
    {
        if (zone.MapId != Zone038MapId)
            return; // whole call is a no-op for every other map

        if (worldState is null || worldState.World.Zone038WinTribe is not { } winningTribe)
            return; // sentinel -- no winner recorded yet

        // Side effect §1: force-reset wipes the whole tribe-guard region before re-planting the winner's posts.
        // Placed after the no-victor guard (legacy returns before the wipe on the no-victor sentinel).
        if (forceFirstPass)
            TribeGuardForceResetSweep.Wipe(zone, Zone038WinnerPoolServerIndexBase);

        foreach (var post in catalog.Zone038WinnerPosts)
        {
            if (post.MapId != zone.MapId || post.TribeId != winningTribe)
                continue;

            EvaluatePost(zone, state, post, Zone038WinnerPoolServerIndexBase, forceFirstPass);
        }
    }

    /// <summary>
    ///     Common tail shared by both pools (<c>S10_MySummon.cpp:1809-1856</c>): a post with no matching
    ///     template is skipped as a whole block; each of its slots is otherwise "occupied? leave alone :
    ///     forced-or-elapsed? spawn : leave empty". A slot's cooldown is armed (set to the template's
    ///     <c>SummonTime1</c>, converted to legacy ticks) the first time an evaluation notices it empty, then
    ///     decremented every real tick by <see cref="Simulate" /> until it reaches zero.
    /// </summary>
    private void EvaluatePost(Zone zone, GuardZoneState state, GuardPostDefinition post, int poolServerIndexBase,
        bool forceFirstPass)
    {
        if (!TryFindTemplate(post.MonsterMainType, post.MonsterSpecialType, out var template))
            return; // no monster template matches this post's (main type, special type) pair -- skip the block

        foreach (var slot in post.Slots)
        {
            var serverIndex = poolServerIndexBase + slot.ReservedSlotIndex;

            if (zone.TryGetMonster(serverIndex, out _))
                continue; // already alive -- left untouched, force or not

            if (!forceFirstPass)
            {
                var slotState = GetOrAddSlot(state, serverIndex);
                if (!slotState.Armed)
                {
                    slotState.Armed = true;
                    slotState.RespawnTicksRemaining =
                        SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(template.SummonTime1));
                }

                if (slotState.RespawnTicksRemaining > 0)
                    continue; // still cooling down
            }

            SpawnGuard(zone, serverIndex, slot, template);

            if (state.Slots.TryGetValue(serverIndex, out var spawned))
                spawned.Armed = false;
        }
    }

    private static GuardSlotRuntimeState GetOrAddSlot(GuardZoneState state, int serverIndex)
    {
        if (!state.Slots.TryGetValue(serverIndex, out var slotState))
        {
            slotState = new GuardSlotRuntimeState();
            state.Slots[serverIndex] = slotState;
        }

        return slotState;
    }

    private bool TryFindTemplate(byte mainType, byte specialType, [NotNullWhen(true)] out MonsterRowDto? template)
    {
        foreach (var definition in worldData.MonstersById.Values)
            if (definition.Monster.Type == mainType && definition.Monster.SpecialType == specialType)
            {
                template = definition.Monster;
                return true;
            }

        template = null;
        return false;
    }

    private static void SpawnGuard(Zone zone, int serverIndex, GuardSlotCoordinate slot, MonsterRowDto template)
    {
        var entity = MonsterEntity.Create(serverIndex, zone.NextMonsterUniqueNumber(), template, serverIndex,
            slot.X, slot.Y, slot.Z, GuardLeashRadius);
        zone.SpawnMonster(entity);
    }
}
