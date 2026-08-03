using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public interface IRegularWarEventSink
{
    public void OnCountdownAnnounced(short mapId, int remainingMinutes);

    public void OnCountdownFinished(short mapId);

    public void OnGateOpened(short mapId);

    public void OnSmallestTribeFlagged(short mapId, byte tribeId);

    public void OnActiveWarStarted(short mapId, int durationLegacyTicks);

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn);

    public void OnReturnToTownAnnounced(short mapId);

    public void OnMonstersShouldDespawn(short mapId);

    public void OnAllSessionsShouldDisconnect(short mapId);
}

public sealed class LoggingRegularWarEventSink(ILogger<LoggingRegularWarEventSink> logger) : IRegularWarEventSink
{
    public void OnCountdownAnnounced(short mapId, int remainingMinutes)
    {
        logger.LogInformation("RegularWar {MapId}: countdown announce, {RemainingMinutes} minute(s) remaining",
            mapId, remainingMinutes);
    }

    public void OnCountdownFinished(short mapId)
    {
        logger.LogInformation("RegularWar {MapId}: countdown finished -- gate-open wait started", mapId);
    }

    public void OnGateOpened(short mapId)
    {
        logger.LogInformation("RegularWar {MapId}: gate opened", mapId);
    }

    public void OnSmallestTribeFlagged(short mapId, byte tribeId)
    {
        logger.LogInformation("RegularWar {MapId}: smallest present tribe flagged -- tribe {TribeId}", mapId,
            tribeId);
    }

    public void OnActiveWarStarted(short mapId, int durationLegacyTicks)
    {
        logger.LogInformation(
            "RegularWar {MapId}: active capture/score window started for {DurationLegacyTicks} legacy tick(s)",
            mapId, durationLegacyTicks);
    }

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
    {
        logger.LogInformation(
            "RegularWar {MapId}: concluded -- outcome={Outcome} winningTribe={WinningTribe} participants={ParticipantCount} bossSpawnDue={BossSpawnDue}",
            mapId, outcome, winningTribe, rewards.Length, bossMonstersShouldSpawn);
    }

    public void OnReturnToTownAnnounced(short mapId)
    {
        logger.LogInformation("RegularWar {MapId}: return-to-town announced", mapId);
    }

    public void OnMonstersShouldDespawn(short mapId)
    {
        logger.LogInformation("RegularWar {MapId}: summoned monsters should despawn", mapId);
    }

    public void OnAllSessionsShouldDisconnect(short mapId)
    {
        logger.LogInformation("RegularWar {MapId}: forced-reset disconnect due for every session on this map",
            mapId);
    }
}
