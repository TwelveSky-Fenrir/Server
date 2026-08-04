namespace Fenrir.Application.Game.Domain.Mounts;

[Flags]
public enum MountLifecycleEffect : byte
{
    None = 0,
    MarkProgressionDirty = 1,
    SendActivityExperienceResponse = 2,
    RecalculateStats = 4,
    BroadcastExperienceCap = 8
}

public enum MountExperienceCreditOutcome : byte
{
    Ineligible,
    MissingAuthoritativeItemSort,
    SpecialMountBlocked,
    InvalidZoneMultiplier,
    NoChange,
    Applied
}

public readonly record struct MountExperienceCreditRequest(
    bool IsMounted,
    int Activity,
    int CurrentExperience,
    bool DoubleExperienceActive,
    bool SessionExperienceUpActive,
    int BaseExperience,
    float ZoneExperienceMultiplier,
    int? AuthoritativeItemSort);

public readonly record struct MountExperienceCreditResult(
    MountExperienceCreditOutcome Outcome,
    int CreditedExperience,
    int NewExperience,
    MountLifecycleEffect Effects)
{
    public bool IsApplied => Outcome == MountExperienceCreditOutcome.Applied;

    public bool RequiresStatRecalculation => (Effects & MountLifecycleEffect.RecalculateStats) != 0;

    public static MountExperienceCreditResult Rejected(MountExperienceCreditOutcome outcome, int currentExperience)
    {
        return new MountExperienceCreditResult(outcome, 0, MountActivityExpCodec.ClampExp(currentExperience),
            MountLifecycleEffect.None);
    }
}

public static class MountExperienceCreditResolver
{
    public static MountExperienceCreditResult Resolve(in MountExperienceCreditRequest request)
    {
        var activity = MountActivityExpCodec.ClampActivity(request.Activity);
        var currentExperience = MountActivityExpCodec.ClampExp(request.CurrentExperience);

        if (!request.IsMounted || activity <= 0 || currentExperience >= MountActivityExpCodec.MaxExp)
            return MountExperienceCreditResult.Rejected(MountExperienceCreditOutcome.Ineligible, currentExperience);

        if (request.AuthoritativeItemSort is not { } itemSort)
            return MountExperienceCreditResult.Rejected(MountExperienceCreditOutcome.MissingAuthoritativeItemSort,
                currentExperience);

        if (itemSort == MountAnimalSortClassifier.NewMountItemSort)
            return MountExperienceCreditResult.Rejected(MountExperienceCreditOutcome.SpecialMountBlocked,
                currentExperience);

        if (!float.IsFinite(request.ZoneExperienceMultiplier) || request.ZoneExperienceMultiplier <= 0f)
            return MountExperienceCreditResult.Rejected(MountExperienceCreditOutcome.InvalidZoneMultiplier,
                currentExperience);

        var rawGain = MountKillExperienceCalculator.ComputeGain(true, activity, currentExperience,
            request.DoubleExperienceActive, request.SessionExperienceUpActive, request.BaseExperience);
        var credited = ApplyZoneMultiplier(rawGain, request.ZoneExperienceMultiplier);
        if (credited <= 0)
            return MountExperienceCreditResult.Rejected(MountExperienceCreditOutcome.NoChange, currentExperience);

        var newExperience = MountActivityExpCodec.ClampExp(SaturatingAdd(currentExperience, credited));
        var applied = newExperience - currentExperience;
        if (applied <= 0)
            return MountExperienceCreditResult.Rejected(MountExperienceCreditOutcome.NoChange, currentExperience);

        var reachedCap = newExperience == MountActivityExpCodec.MaxExp;
        var effects = MountLifecycleEffect.MarkProgressionDirty | MountLifecycleEffect.SendActivityExperienceResponse;
        if (reachedCap)
            effects |= MountLifecycleEffect.RecalculateStats | MountLifecycleEffect.BroadcastExperienceCap;

        return new MountExperienceCreditResult(MountExperienceCreditOutcome.Applied, applied, newExperience, effects);
    }

    private static int ApplyZoneMultiplier(int amount, float multiplier)
    {
        if (amount <= 0)
            return 0;

        var scaled = amount * (double)multiplier;
        return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
    }

    private static int SaturatingAdd(int left, int right)
    {
        var sum = (long)left + right;
        return sum >= int.MaxValue ? int.MaxValue : (int)sum;
    }
}
