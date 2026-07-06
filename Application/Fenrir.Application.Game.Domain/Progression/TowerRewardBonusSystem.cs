using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Recomputes the 4 per-tribe tower reward bonuses every tick -- the Fenrir counterpart of legacy's
///     <c>UpdateTowerBonus()</c> (<c>Server/ts25zone/S07_MyGame01.cpp:2653-2659</c>), which every
///     <c>ts25zone.exe</c> process calls unconditionally each tick regardless of whether that process hosts
///     one of the 12 towers -- unlike <c>ProcessForTower</c> (<see cref="TowerGuardianSystem" />), which only
///     runs for the 12 tower-hosting zones. Registered for every zone the same way, so this recomputes
///     redundantly once per zone's own tick rather than once per shard process; harmless, since
///     <see cref="TowerWarState.RecomputeTribeBonuses" /> is a cheap (12-integer) pure re-derivation from
///     already-shared state, not a per-zone-local computation.
/// </summary>
public sealed class TowerRewardBonusSystem(TowerWarState towerWar) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        _ = zone;
        _ = legacyTicksElapsed;
        towerWar.RecomputeTribeBonuses();
    }
}
