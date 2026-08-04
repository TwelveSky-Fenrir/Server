using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class ZoneZone175MissionEffects(
    Zone zone,
    Zone175InstanceConfig config,
    MonsterSpawnScheduler monsterSpawnScheduler,
    Lazy<ZoneCenterBroadcastIngestor> centerBroadcastIngestor,
    ILogger logger) : IZone175MissionEffects
{
    private bool _stageDataUnavailableLogged;

    public bool AnyQualifyingPlayerPresent()
    {
        return zone.HasAnyZone175QualifyingPlayer();
    }

    public int CountLivingWaveBosses(int stage)
    {
        return zone.CountLivingZone175WaveBosses(Zone175RewardTables.WaveBossSpecialType(stage));
    }

    public bool TryLoadWaveStage(int stage)
    {
        if (monsterSpawnScheduler.TryLoadZone175MissionStage(zone, stage))
            return true;

        if (_stageDataUnavailableLogged)
            return false;

        _stageDataUnavailableLogged = true;
        logger.LogWarning(
            "Zone175 mission stage {Stage} on map {MapId} cannot start because its staged spawn rows or required boss are unavailable; state progression is held without rewards",
            stage, zone.MapId);
        return false;
    }

    public void MaintainWaveStage()
    {
    }

    public void RemoveMissionMonsters()
    {
        monsterSpawnScheduler.ClearZone175MissionStage(zone);
    }

    public void RewardQualifyingPlayers(int stage)
    {
        zone.GrantZone175WaveReward(stage, config.ExperienceRatio);
    }

    public void ForceDisconnectAll()
    {
        zone.ForceDisconnectAllForZone175();
    }

    public void PublishStateChange(int eventCode, int value = 0)
    {
        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, config.Index1);
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..], config.Index2);
        BinaryPrimitives.WriteInt32LittleEndian(payload[8..], value);
        centerBroadcastIngestor.Value.Ingest(eventCode, payload);
    }

    public void Notify(Zone175MissionEvent missionEvent, int wave, int remaining)
    {
        logger.LogInformation(
            "Zone175 mission {Event} on map {MapId} cell={Index1}/{Index2} wave={Wave} remaining={Remaining}",
            missionEvent, zone.MapId, config.Index1, config.Index2, wave, remaining);
    }
}
