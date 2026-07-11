using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     A11 -- drives the periodic per-player tower-war info push (legacy per-player maintenance pass,
///     S07_MyGame04.cpp:835-837,882-884) on the zone's own tick thread. Self-contained and stateless: it only
///     forwards the elapsed legacy-tick count to <see cref="Zone.TickTowerInfoPush" />, which owns the per-zone
///     accrual counter and the 30-second cadence -- see that method's own remarks for the cadence and payload
///     rationale. Registered once as an <c>ISimulationSystem</c> singleton and shared across every zone (the push
///     no-ops on non-tower zones), so it carries no per-zone state of its own.
/// </summary>
public sealed class TowerInfoPushSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        zone.TickTowerInfoPush(legacyTicksElapsed);
    }
}
