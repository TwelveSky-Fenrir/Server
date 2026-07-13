namespace Fenrir.Application.Game.Domain.Combat;

public readonly record struct ReflectOutcome(bool DestroyerSucceeded, bool ReflectFired, int ReflectDamage);

public static class ReflectResolver
{
    public const int DestroyerBuffSlot = 14;

    public const int ReflectBuffSlot = 12;

    private const int DestroyerActiveMin = 1;
    private const int DestroyerActiveMax = 201;
    private const int DestroyerRollBase = 1000;

    private const int ReflectActiveMin = 1;
    private const int ReflectActiveMax = 150;
    private const int ReflectProbabilityRollBase = 1500;
    private const int ReflectGateRollBase = 5;
    private const int ReflectLevelPenaltyPerLevel = 3;

    public static ReflectOutcome Resolve(int attackerDestroyerBuff, int defenderReflectBuff, int attackerLevel,
        int defenderLevel, int attackerAntiReflectStat, bool reflectEnabled, int preElementMainDamage,
        IRandomSource rng)
    {
        var destroyerSucceeded = false;
        if (attackerDestroyerBuff is >= DestroyerActiveMin and <= DestroyerActiveMax)
            destroyerSucceeded = rng.NextInt32(DestroyerRollBase) < attackerDestroyerBuff;

        var reflectFired = false;
        if (reflectEnabled && defenderReflectBuff is >= ReflectActiveMin and <= ReflectActiveMax)
        {
            var probability = defenderReflectBuff;
            if (attackerLevel > defenderLevel)
                probability -= ReflectLevelPenaltyPerLevel * (attackerLevel - defenderLevel);
            probability -= attackerAntiReflectStat;
            if (probability < 0)
                probability = 0;

            var probabilityRoll = rng.NextInt32(ReflectProbabilityRollBase);
            var gateRoll = rng.NextInt32(ReflectGateRollBase);
            reflectFired = gateRoll == 0 && probabilityRoll < probability;
        }

        var reflectDamage = reflectFired ? preElementMainDamage * 3 / 2 : 0;
        return new ReflectOutcome(destroyerSucceeded, reflectFired, reflectDamage);
    }
}
