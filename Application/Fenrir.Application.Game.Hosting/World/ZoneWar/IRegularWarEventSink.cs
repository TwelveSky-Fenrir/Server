using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     The seam between <see cref="RegularWarSchedulerHost" /> (which only ever knows "this happened, on this
///     map") and whatever actually mutates live game state (money/xp/CP grants, session disconnects, monster
///     spawn/despawn, ground-item drops). Kept as its own interface so the schedule/reward math
///     (<see cref="RegularWarSchedule" />/<see cref="RegularWarRewardCalculator" />) can be exercised,
///     end-to-end, against a completely inert default before any of those real mutations are wired up.
/// </summary>
public interface IRegularWarEventSink
{
    void OnCountdownAnnounced(short mapId, int remainingMinutes);

    void OnSmallestTribeFlagged(short mapId, byte tribeId);

    void OnActiveWarStarted(short mapId);

    void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn);

    void OnMonstersShouldDespawn(short mapId);

    void OnAllSessionsShouldDisconnect(short mapId);
}

/// <summary>
///     Production-safe default: observes and logs every event, mutates nothing. Registered by default so
///     <see cref="RegularWarSchedulerHost" /> can run continuously (same "real host, currently a no-op until
///     something drives it" posture as <c>Fenrir.Application.Game.Hosting.World.ZoneWar.ZoneWarTickService</c>
///     before <c>StartWar</c> is ever called) without risking the forced-reset "disconnect every session on the
///     map" side effect, or granting rewards computed from <see cref="UnavailableRegularWarRewardValueProvider" />'s
///     always-zero money/xp table, against real players before either is genuinely ready.
/// </summary>
/// <remarks>
///     Swapping in a real, Zone-backed sink (queuing money grants, mutating <c>PlayerRuntimeState</c> CP/XP/hero
///     points, dropping ground items, disconnecting sessions, spawning the boss-war monsters) is the deliberate
///     follow-up integration step once: (1) the real 12-row money table and <c>GetBattleExpReward</c> formula
///     are available via a fresh legacy-behavior-translator contract (see
///     <see cref="IRegularWarRewardValueProvider" />'s remarks), and (2) that integration can safely touch
///     <c>Zone</c>'s own tick-thread-owned mutation surface, which this cluster deliberately left untouched to
///     avoid colliding with concurrent Combat/Commerce/GM-tooling work in the same files.
/// </remarks>
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
