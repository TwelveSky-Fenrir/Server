using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

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
