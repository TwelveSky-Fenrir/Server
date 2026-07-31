using System.Collections.Concurrent;
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

public sealed class LoggingOnlyZone039MonsterSummonResetGateway(
    ILogger<LoggingOnlyZone039MonsterSummonResetGateway> logger) : IZone039MonsterSummonResetGateway
{
    public void ResetGeneralSpawnTable(Zone zone)
    {
        logger.LogWarning(
            "Zone039Arming: zone {MapId} should force-invalidate its general monster spawn table for a fresh " +
            "re-summon cycle (legacy ResetMonsterSummonState), but no real MonsterSpawnScheduler reset hook is " +
            "wired yet -- see Zone039ArmingReactor's remarks / this task's wiringManifest", zone.MapId);
    }
}
