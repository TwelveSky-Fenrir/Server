namespace Fenrir.Application.Game.Domain.Combat;

public static class DarkAttackPotionResolver
{
    private const int RollRange = 100;

    public static bool ShouldApply(bool attackerBuffActive, int attackerBuffProbability,
        bool defenderAlreadyAffected, IRandomSource rng)
    {
        if (!attackerBuffActive || defenderAlreadyAffected)
            return false;

        return rng.NextInt32(RollRange) < attackerBuffProbability;
    }
}
