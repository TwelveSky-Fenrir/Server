namespace Fenrir.Application.Game.Domain.Simulation;

public interface IZone175MissionEffects
{
    public bool AnyQualifyingPlayerPresent();

    public int CountLivingWaveBosses(int stage);

    public bool TryLoadWaveStage(int stage);

    public void MaintainWaveStage();

    public void RemoveMissionMonsters();

    public void RewardQualifyingPlayers(int stage);

    public void ForceDisconnectAll();

    public void PublishStateChange(int eventCode, int value = 0);

    public void Notify(Zone175MissionEvent missionEvent, int wave, int remaining);
}

public sealed class NullZone175MissionEffects : IZone175MissionEffects
{
    public static readonly NullZone175MissionEffects Instance = new();

    public bool AnyQualifyingPlayerPresent()
    {
        return false;
    }

    public int CountLivingWaveBosses(int stage)
    {
        return 0;
    }

    public bool TryLoadWaveStage(int stage)
    {
        return false;
    }

    public void MaintainWaveStage()
    {
    }

    public void RemoveMissionMonsters()
    {
    }

    public void RewardQualifyingPlayers(int stage)
    {
    }

    public void ForceDisconnectAll()
    {
    }

    public void PublishStateChange(int eventCode, int value = 0)
    {
    }

    public void Notify(Zone175MissionEvent missionEvent, int wave, int remaining)
    {
    }
}
