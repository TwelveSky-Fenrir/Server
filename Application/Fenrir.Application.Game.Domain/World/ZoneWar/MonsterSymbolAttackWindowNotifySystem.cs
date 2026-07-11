using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class MonsterSymbolAttackWindowNotifySystem(
    WorldStateService worldState,
    MonsterSymbolAttackWindowTracker tracker,
    Lazy<ZoneEventBroadcaster> broadcaster,
    IOptions<GameServerOptions> options) : ISimulationSystem
{

        private const int LegacyTicksPerMinute = 120;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var opts = options.Value;
        if (!opts.MonsterSymbolAttackNotifyEnabled)
            return;

        if (worldState.World.MonsterSymbol is not { } holder)
            return;

        if (!opts.MonsterSymbolAttackNotifyMapIds.TryGetValue(holder, out var targetMapId) ||
            targetMapId != zone.MapId)
            return;

        var delayLegacyTicks = opts.MonsterSymbolAttackNotifyDelayMinutes * LegacyTicksPerMinute;

        if (tracker.ShouldNotifyNow(holder, legacyTicksElapsed, delayLegacyTicks))
            broadcaster.Value.AnnounceMonsterSymbolAttackWindow();
    }
}
