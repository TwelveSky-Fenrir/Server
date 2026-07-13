using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class ZoneZone175MissionEffects(Zone zone, Zone175InstanceConfig config, ILogger logger)
    : IZone175MissionEffects
{
    private bool _summonGapLogged;

    public bool AnyQualifyingPlayerPresent()
    {
        return zone.HasAnyZone175QualifyingPlayer();
    }

    public int CountLivingWaveBosses(int stage)
    {
        return zone.CountLivingZone175WaveBosses(Zone175RewardTables.WaveBossSpecialType(stage));
    }

    public void SummonWaveBoss(int stage)
    {
        LogSummonGapOnce();
    }

    public void SummonTrickle(int stage)
    {
        LogSummonGapOnce();
    }

    public void RemoveMissionMonsters()
    {
        zone.RemoveZone175MissionMonsters();
    }

    public void RewardQualifyingPlayers(int stage)
    {
        zone.GrantZone175WaveReward(stage, config.ExperienceRatio);
    }

    public void ForceDisconnectAll()
    {
        zone.ForceDisconnectAllForZone175();
    }

    public void Notify(Zone175MissionEvent missionEvent, int wave, int remaining)
    {
        logger.LogInformation(
            "Zone175 Labyrinth {Event} on map {MapId} (index1={Index1} index2={Index2}) wave={Wave} remaining={Remaining}",
            missionEvent, zone.MapId, config.Index1, config.Index2, wave, remaining);
    }

    private void LogSummonGapOnce()
    {
        if (_summonGapLogged)
            return;

        _summonGapLogged = true;
        logger.LogWarning(
            "Zone175 Labyrinth on map {MapId}: wave-boss/trickle summoning is a GAP (concrete monster ids are " +
            "not recovered from the source contract) -- no monsters are being summoned. The mission lifecycle " +
            "still advances (wave-clear detection currently sees zero bosses immediately). Recover the ids via a " +
            "cpp-zone-gameplay-analyst follow-up before enabling this map.",
            zone.MapId);
    }
}
