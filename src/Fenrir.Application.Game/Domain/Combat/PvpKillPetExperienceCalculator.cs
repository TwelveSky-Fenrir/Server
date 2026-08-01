namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillPetExperienceCalculator
{
    public const int DefaultPetExpRatio = 200;

    public const int MinimumAttackerBaseLevel = 113;

    public static int ComputeGain(int attackerBaseLevel, int petExpRatio = DefaultPetExpRatio)
    {
        return attackerBaseLevel < MinimumAttackerBaseLevel ? 0 : petExpRatio;
    }
}
