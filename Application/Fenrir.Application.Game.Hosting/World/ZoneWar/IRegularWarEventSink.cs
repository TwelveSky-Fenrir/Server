using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public interface IRegularWarEventSink
{
    public void OnCountdownAnnounced(short mapId, int remainingMinutes);

    public void OnSmallestTribeFlagged(short mapId, byte tribeId);

    public void OnActiveWarStarted(short mapId);

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn);

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

    public void OnSmallestTribeFlagged(short mapId, byte tribeId)
    {
        logger.LogInformation("RegularWar {MapId}: smallest present tribe flagged -- tribe {TribeId}", mapId,
            tribeId);
    }

    public void OnActiveWarStarted(short mapId)
    {
        logger.LogInformation("RegularWar {MapId}: active capture/score window started", mapId);
    }

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
    {
        logger.LogInformation(
            "RegularWar {MapId}: concluded -- outcome={Outcome} winningTribe={WinningTribe} participants={ParticipantCount} bossSpawnDue={BossSpawnDue}",
            mapId, outcome, winningTribe, rewards.Length, bossMonstersShouldSpawn);
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
