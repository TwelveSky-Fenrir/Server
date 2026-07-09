using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-wide home for each hosted Valley of the Deceased map's own <see cref="ValleyWarSchedule" /> --
///     the bridge between <see cref="ValleyWarSystem" /> (which ticks the schedule once per zone tick) and
///     <see cref="Monsters.MonsterSpawnScheduler" />'s own per-kill credit path (which needs to reach the SAME
///     schedule instance to decrement a tribe's kill quota, contract §6 -- Server/ts25zone/S07_MyGame02.cpp:3162-3170).
/// </summary>
/// <remarks>
///     Every entry is only ever mutated from its own map's zone tick thread: <see cref="ValleyWarSystem.Simulate" />
///     and <see cref="Monsters.MonsterSpawnScheduler.Simulate" /> both run, for a given <see cref="Zone" />, as
///     part of that SAME zone's own single-threaded <see cref="Zone.Tick" /> call -- never concurrently for the
///     same map id, same single-writer-per-zone invariant every other cross-cutting process-wide singleton in
///     this cluster (<see cref="Monsters.MonsterSpawnScheduler" />'s own <c>_stateByZone</c>,
///     <see cref="RegularWarActiveMapTracker" />) already relies on. <see cref="ConcurrentDictionary{TKey,TValue}" />
///     is used only so two DIFFERENT maps' first-ever <see cref="GetOrCreate" /> calls (on their own,
///     genuinely-concurrent zone threads) never race each other's dictionary insert.
/// </remarks>
public sealed class ValleyWarKillRegistry
{
    private readonly ConcurrentDictionary<short, ValleyWarSchedule> _schedules = new();

    public ValleyWarSchedule GetOrCreate(short mapId)
    {
        return _schedules.GetOrAdd(mapId, static _ => new ValleyWarSchedule());
    }

    /// <summary>No-op for a map this catalog does not configure, or one with no schedule created yet.</summary>
    public void RegisterMonsterKill(short mapId, byte tribeId)
    {
        if (!ValleyWarMapCatalog.Contains(mapId))
            return;

        GetOrCreate(mapId).RegisterMonsterKill(tribeId);
    }
}
