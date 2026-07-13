namespace Fenrir.Application.Game.Domain.Combat;

public readonly record struct Zone124OverrideOutcome(int Damage, bool CritExists);

public static class Zone124DuelOverrideResolver
{
    public const short Zone124MapId = 124;

    public const int FinalCountdownThreshold = 10;

    public const int DamageMultiplier = 3;

    public static bool IsActive(bool isMap124Process, int countdownRemaining)
    {
        return isMap124Process && countdownRemaining < FinalCountdownThreshold;
    }

    public static Zone124OverrideOutcome Apply(int damage, bool critExists, bool isMap124Process,
        int countdownRemaining)
    {
        return IsActive(isMap124Process, countdownRemaining)
            ? new Zone124OverrideOutcome(damage * DamageMultiplier, true)
            : new Zone124OverrideOutcome(damage, critExists);
    }
}
