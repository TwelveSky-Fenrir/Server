using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     C15-regwar-reward wiring seam: fans one <see cref="IRegularWarEventSink" /> lifecycle event out to every
///     entry in <paramref name="sinks" />, in order, isolating each inner sink's own failure (logged, skipped)
///     so one broken sink can never suppress delivery to the others -- the same "one bad unit must never take
///     the rest down" posture <c>Zone.DrainInbox</c>'s own per-command try/catch already established elsewhere
///     in this codebase. Exists specifically because <see cref="RegularWarSchedulerHost" />'s constructor only
///     ever accepts a single <see cref="IRegularWarEventSink" />, but two independent, unrelated concerns both
///     need to observe the exact same event stream: <see cref="ZoneCenterRegularWarEventSink" /> (siege-state
///     relay to center) and <see cref="RegularWarRewardGrantSink" /> (C15's real money/xp/CP/hero-rank/
///     item-drop reward payout). See this cluster's wiring report for the DI registration this composes.
/// </summary>
public sealed class CompositeRegularWarEventSink(
    IReadOnlyList<IRegularWarEventSink> sinks,
    ILogger<CompositeRegularWarEventSink> logger) : IRegularWarEventSink
{
    public void OnCountdownAnnounced(short mapId, int remainingMinutes)
    {
        ForEach(sink => sink.OnCountdownAnnounced(mapId, remainingMinutes), nameof(OnCountdownAnnounced));
    }

    public void OnSmallestTribeFlagged(short mapId, byte tribeId)
    {
        ForEach(sink => sink.OnSmallestTribeFlagged(mapId, tribeId), nameof(OnSmallestTribeFlagged));
    }

    public void OnActiveWarStarted(short mapId)
    {
        ForEach(sink => sink.OnActiveWarStarted(mapId), nameof(OnActiveWarStarted));
    }

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
    {
        ForEach(sink => sink.OnWarConcluded(mapId, outcome, winningTribe, rewards, bossMonstersShouldSpawn),
            nameof(OnWarConcluded));
    }

    public void OnMonstersShouldDespawn(short mapId)
    {
        ForEach(sink => sink.OnMonstersShouldDespawn(mapId), nameof(OnMonstersShouldDespawn));
    }

    public void OnAllSessionsShouldDisconnect(short mapId)
    {
        ForEach(sink => sink.OnAllSessionsShouldDisconnect(mapId), nameof(OnAllSessionsShouldDisconnect));
    }

    private void ForEach(Action<IRegularWarEventSink> action, string eventName)
    {
        foreach (var sink in sinks)
            try
            {
                action(sink);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RegularWar event sink {SinkType} failed handling {Event}",
                    sink.GetType().Name, eventName);
            }
    }
}
