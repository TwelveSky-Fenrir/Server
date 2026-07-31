namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillPetExperienceCalculator
{
    public const int DefaultPetExpRatio = 200;

    private const int LevelFloor = ExperienceFormulas.RebirthDivisorLevelThreshold;

    public static int ComputeGain(int attackerCombinedLevel, int petExpRatio = DefaultPetExpRatio)
    {
        return attackerCombinedLevel < LevelFloor ? 0 : petExpRatio;
    }
}
