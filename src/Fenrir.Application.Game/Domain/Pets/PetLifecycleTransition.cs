using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Pets;

[Flags]
public enum PetLifecycleEffect : byte
{
    None = 0,
    SynchronizeEquippedItem = 1,
    MarkProgressionDirty = 2,
    RecalculateStats = 4,
    SendActivityResponse = 8,
    BroadcastGrowthTier = 16
}

public readonly record struct PetLifecycleTransition(
    int NewGrowth,
    byte NewActivity,
    int NewDecayAccrualTicks,
    int CreditedGrowth,
    bool TierIncreased,
    PetLifecycleEffect Effects)
{
    public bool GrowthChanged => CreditedGrowth > 0;

    public bool ActivityChanged => (Effects & PetLifecycleEffect.SendActivityResponse) != 0;

    public bool RequiresStatRecalculation => (Effects & PetLifecycleEffect.RecalculateStats) != 0;

    public bool RequiresGrowthTierBroadcast => (Effects & PetLifecycleEffect.BroadcastGrowthTier) != 0;

    public static PetLifecycleTransition Unchanged(int growth, byte activity, int decayAccrualTicks)
    {
        return new PetLifecycleTransition(growth, activity, decayAccrualTicks, 0, false, PetLifecycleEffect.None);
    }
}

public static class PetLifecycleTransitionResolver
{
    public static PetLifecycleTransition ResolveExperience(int currentGrowth, byte currentActivity,
        in PetExperienceCreditResult credit)
    {
        if (!credit.IsEligible)
            return PetLifecycleTransition.Unchanged(currentGrowth, currentActivity, 0);

        var growthChanged = credit.CreditedAmount > 0 && credit.NewGrowth != currentGrowth;
        var newActivity = (byte)Math.Clamp(credit.NewActivity, 0, ItemQuantityPolicy.MaxPetActivity);
        var activityChanged = newActivity != currentActivity;

        if (!growthChanged && !activityChanged)
            return PetLifecycleTransition.Unchanged(currentGrowth, currentActivity, 0);

        var effects = PetLifecycleEffect.SynchronizeEquippedItem | PetLifecycleEffect.MarkProgressionDirty;
        if (growthChanged || CrossedActivityThreshold(currentActivity, newActivity))
            effects |= PetLifecycleEffect.RecalculateStats;
        if (activityChanged)
            effects |= PetLifecycleEffect.SendActivityResponse;
        if (credit.TierIncreased)
            effects |= PetLifecycleEffect.BroadcastGrowthTier;

        return new PetLifecycleTransition(credit.NewGrowth, newActivity, 0,
            growthChanged ? credit.CreditedAmount : 0, credit.TierIncreased, effects);
    }

    public static PetLifecycleTransition ResolveDecay(int currentGrowth, byte currentActivity,
        int currentAccrualTicks, int legacyTicksElapsed, int decayIntervalTicks, bool decaySuppressed)
    {
        if (currentActivity == 0 || legacyTicksElapsed <= 0)
            return PetLifecycleTransition.Unchanged(currentGrowth, currentActivity,
                Math.Max(0, currentAccrualTicks));

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(decayIntervalTicks, 0);

        var totalTicks = Math.Min((long)Math.Max(0, currentAccrualTicks) + legacyTicksElapsed, int.MaxValue);
        var elapsedDecayPeriods = (int)(totalTicks / decayIntervalTicks);
        var accrualRemainder = (int)(totalTicks % decayIntervalTicks);

        if (elapsedDecayPeriods == 0 || decaySuppressed)
            return PetLifecycleTransition.Unchanged(currentGrowth, currentActivity, accrualRemainder);

        var newActivity = (byte)Math.Max(0, currentActivity - elapsedDecayPeriods);
        var effects = PetLifecycleEffect.SynchronizeEquippedItem |
                      PetLifecycleEffect.MarkProgressionDirty |
                      PetLifecycleEffect.SendActivityResponse;

        if (CrossedActivityThreshold(currentActivity, newActivity))
            effects |= PetLifecycleEffect.RecalculateStats;

        return new PetLifecycleTransition(currentGrowth, newActivity, accrualRemainder, 0, false, effects);
    }

    private static bool CrossedActivityThreshold(byte before, byte after)
    {
        return before > 0 && after == 0;
    }
}
