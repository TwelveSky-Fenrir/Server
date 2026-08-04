using Fenrir.Application.Game.Domain.Simulation;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class Zone38BossScheduleSystem(
    Zone38BossSchedule schedule,
    IZone38BossScheduleEffects effects,
    IOptions<GameServerOptions> options,
    TimeProvider? timeProvider = null) : ISimulationSystem
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0 || !options.Value.HolyStoneWarEnabled ||
            zone.MapId != Zone38BossSchedule.ZoneId)
            return;

        schedule.Tick(zone, _timeProvider.GetLocalNow(), effects);
    }
}
