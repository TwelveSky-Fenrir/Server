namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillPetExperienceCalculator
{
    public const int PlaceholderPetExpRatio = 10;

    private const int LevelFloor = ExperienceFormulas.RebirthDivisorLevelThreshold;

    public static int ComputeGain(int attackerCombinedLevel, int petExpRatio = PlaceholderPetExpRatio)
    {
        return attackerCombinedLevel < LevelFloor ? 0 : petExpRatio;
    }
}
