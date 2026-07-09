using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     "Monster symbol" (<c>mYaoguaiHSB</c>) timer notify: once the neutral, monster-guarded battle symbol's
///     current holder tribe has kept it for the configured delay, broadcasts a single, payload-less sort-401
///     notice -- gated so only the one zone instance mapped to the current holder tribe ever proceeds.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:2622-2651 (per-tick gate on <c>mSERVER_INFO.mYaoguaiHSB</c>;
///     holder-tribe-to-server-number mapping 0-&gt;4, 1-&gt;9, 2-&gt;14, 3-&gt;143; only the matching instance
///     proceeds; <c>bAttackMonsterSymbol</c>/start-timestamp gate the one-shot <c>Broadcast(401)</c>).
///     <para>
///         Runs on every zone's own tick (cheap: a disabled-flag check, then a dictionary lookup, then a single
///         map-id comparison) but only ever does real work on the one zone, if any, currently matching the
///         mapped instance for whichever tribe holds the symbol right now -- every other zone this shard hosts
///         returns immediately after the map-id comparison fails, so this never grows more expensive as the
///         shard hosts more maps.
///     </para>
/// </remarks>
public sealed class MonsterSymbolAttackWindowNotifySystem(
    WorldStateService worldState,
    MonsterSymbolAttackWindowTracker tracker,
    ZoneEventBroadcaster broadcaster,
    IOptions<GameServerOptions> options) : ISimulationSystem
{
    /// <summary>Server/Header/function.h:1643-1652: 120 legacy ticks (TimeLogic=500ms) per real minute.</summary>
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
            broadcaster.AnnounceMonsterSymbolAttackWindow();
    }
}
