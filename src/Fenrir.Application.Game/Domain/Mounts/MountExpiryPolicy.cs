namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountExpiryPolicy
{
    public static MountMinuteTransition AdvanceMinute(in MountMinuteState state)
    {
        if (!MountStateResolver.IsMountedSlot(state.AnimalIndex) || state.AnimalNumber <= 0)
            return new MountMinuteTransition(state.AnimalIndex, state.AnimalNumber, Math.Max(0, state.RideTime),
                state.AbsorbState == 0 ? 0 : 1, Math.Max(0, state.AbsorbTime));

        var absorbState = state.AbsorbState == 0 ? 0 : 1;
        var absorbTime = Math.Max(0, state.AbsorbTime);
        var absorptionExpired = false;

        if (absorbState != 0)
        {
            absorbTime = Math.Max(0, absorbTime - 1);
            if (absorbTime == 0)
            {
                absorbState = 0;
                absorptionExpired = true;
            }
        }

        var rideTime = Math.Max(0, state.RideTime - 1);
        if (rideTime != 0)
            return new MountMinuteTransition(state.AnimalIndex, state.AnimalNumber, rideTime, absorbState,
                absorbTime, absorptionExpired);

        return new MountMinuteTransition(Dismounted(state.AnimalIndex), 0, 0, 0, absorbTime,
            absorptionExpired, RideExpired: true);
    }

    public static MountActivityTransition AdvanceThirtySeconds(in MountActivityState state)
    {
        var activity = MountActivityExpCodec.ClampActivity(state.Activity);
        if (!MountStateResolver.IsMountedSlot(state.AnimalIndex) || state.AnimalNumber <= 0)
            return new MountActivityTransition(activity);

        var newActivity = state.DoubleExperienceActive ? activity : Math.Max(0, activity - 1);
        return new MountActivityTransition(newActivity, newActivity != state.Activity);
    }

    public static int Dismounted(int animalIndex)
    {
        return MountStateResolver.IsMountedSlot(animalIndex)
            ? animalIndex - MountStateResolver.SlotCount
            : animalIndex;
    }
}

public readonly record struct MountMinuteState(
    int AnimalIndex,
    int AnimalNumber,
    int RideTime,
    int AbsorbState,
    int AbsorbTime);

public readonly record struct MountMinuteTransition(
    int AnimalIndex,
    int AnimalNumber,
    int RideTime,
    int AbsorbState,
    int AbsorbTime,
    bool AbsorptionExpired = false,
    bool RideExpired = false);

public readonly record struct MountActivityState(
    int AnimalIndex,
    int AnimalNumber,
    int Activity,
    bool DoubleExperienceActive);

public readonly record struct MountActivityTransition(
    int Activity,
    bool ActivityChanged = false);
