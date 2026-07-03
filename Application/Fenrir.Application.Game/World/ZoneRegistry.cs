using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Simulation;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.World;

/// <summary>
///     The process's fixed set of zone actors — one <see cref="Zone" /> per entry of
///     <see cref="GameServerOptions.Maps" /> (ADR-0012: a shard hosts a DISJOINT partition of maps), built once
///     at startup into a <see cref="FrozenDictionary{TKey,TValue}" /> and never mutated after. Everything that
///     used to inject the single M1 <c>Zone</c> singleton injects this instead: routing
///     (<c>EnterWorldHandler</c>) resolves <c>character.MapId</c> here, the tick host runs one task
///     per <see cref="Zones" /> entry, and the process-wide readers (write-behind flush, heartbeat CCU) go
///     through <see cref="TryGetPlayer" />/<see cref="TotalPlayerCount" />.
/// </summary>
public sealed class ZoneRegistry
{
    private readonly FrozenDictionary<short, Zone> _zones;

    public ZoneRegistry(IOptions<GameServerOptions> options, MovementRules movementRules,
        DirtyTracker<int> dirtyTracker, ILogger<Zone> zoneLogger, WorldDataCache worldData,
        IEnumerable<ISimulationSystem> simulationSystems)
    {
        var opts = options.Value;

        // DI registration order IS simulation order (report 05 §0's deterministic legacy sequence) -- every
        // zone shares the exact same ordered list of systems, resolved once here rather than per-zone, since
        // ISimulationSystem instances are stateless singletons that operate on whichever Zone they're handed.
        var systems = simulationSystems.ToImmutableArray();

        _zones = opts.Maps.ToFrozenDictionary(
            mapId => mapId,
            mapId => new Zone(mapId, opts, movementRules, dirtyTracker, systems, zoneLogger, worldData));
    }

    /// <summary>Every hosted zone, in no particular order — the tick host launches one loop per entry.</summary>
    public ImmutableArray<Zone> Zones => _zones.Values;

    /// <summary>
    ///     Direct lookup for a map this process is KNOWN to host — throws for a foreign map, so routing paths
    ///     that must degrade gracefully (world-entry for a character persisted on an unhosted map) use
    ///     <see cref="TryGet" /> instead.
    /// </summary>
    public Zone this[short mapId] => _zones[mapId];

    /// <summary>Sum of every zone's live player count — the directory heartbeat's CCU figure for this shard.</summary>
    public int TotalPlayerCount
    {
        get
        {
            var total = 0;
            foreach (var zone in _zones.Values)
                total += zone.PlayerCount;
            return total;
        }
    }

    public bool TryGet(short mapId, [NotNullWhen(true)] out Zone? zone)
    {
        return _zones.TryGetValue(mapId, out zone);
    }

    /// <summary>
    ///     Cross-zone player lookup for the process-wide readers (write-behind flush). A character lives in AT
    ///     MOST one zone (Enter/Leave/handoff all preserve that, see <see cref="ZoneTransfer" />), so first hit
    ///     wins. False for a player mid-handoff (already left the source, not yet drained by the target) — the
    ///     caller treats that like the logged-out case: skip now, the next flush finds them in the target zone,
    ///     whose Enter re-marks them dirty.
    /// </summary>
    public bool TryGetPlayer(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? state)
    {
        foreach (var zone in _zones.Values)
            if (zone.TryGetPlayer(characterId, out state) && state is not null)
                return true;

        state = null;
        return false;
    }
}
