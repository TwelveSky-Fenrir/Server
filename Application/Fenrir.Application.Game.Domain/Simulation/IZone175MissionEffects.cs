namespace Fenrir.Application.Game.Domain.Simulation;

public interface IZone175MissionEffects
{

        public bool AnyQualifyingPlayerPresent();

        public int CountLivingWaveBosses(int stage);

        public void SummonWaveBoss(int stage);

        public void SummonTrickle(int stage);

        public void RemoveMissionMonsters();

        public void RewardQualifyingPlayers(int stage);

        public void ForceDisconnectAll();

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

    public void SummonWaveBoss(int stage)
    {
    }

    public void SummonTrickle(int stage)
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

    public void Notify(Zone175MissionEvent missionEvent, int wave, int remaining)
    {
    }
}
