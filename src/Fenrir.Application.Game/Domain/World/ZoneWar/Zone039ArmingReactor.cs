using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.World.Monsters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class Zone039ArmingReactor(IZone039MonsterSummonResetGateway gateway)
{
    private readonly ConcurrentDictionary<short, Zone039ArmingState> _stateByZone = new();

    public void Apply(ZoneRegistry zones)
    {
        foreach (var zone in zones.Zones)
        {
            if (!ScheduledZoneCenterEventCodes.Zone039ArmingGatedMapIds.Contains(zone.MapId))
                continue;

            var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new Zone039ArmingState());
            state.BattleState = 1;
            state.PostTick = 0;

            if (zone.MapId == ScheduledZoneCenterEventCodes.Zone039MonsterSummonResetMapId)
                gateway.ResetGeneralSpawnTable(zone);
        }
    }

    public Zone039ArmingState? TryGetState(short mapId)
    {
        return _stateByZone.TryGetValue(mapId, out var state) ? state : null;
    }
}

public sealed class Zone039ArmingState
{
    public int BattleState;
    public int PostTick;
}

public interface IZone039MonsterSummonResetGateway
{
    public void ResetGeneralSpawnTable(Zone zone);
}

public sealed class Zone039MonsterSummonResetGateway(
    MonsterSpawnScheduler spawnScheduler,
    ILogger<Zone039MonsterSummonResetGateway> logger) : IZone039MonsterSummonResetGateway
{
    // Only map 74 resets; there is no mServerNumber == 39 anywhere -- Server/ts25zone/S07_MyGame08.cpp:211-215.
    // Deferred to the zone tick because AoiGrid is not thread-safe and this runs off the tick thread.
    public void ResetGeneralSpawnTable(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (zone.MapId != ScheduledZoneCenterEventCodes.Zone039MonsterSummonResetMapId)
            return;

        spawnScheduler.RequestSummonStateReset(zone);
        logger.LogInformation("Zone039Arming: zone {MapId} general summon state reset armed", zone.MapId);
    }
}
