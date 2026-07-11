namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountActivityExpCodec
{
    public const int ActivityScale = 1_000_000;

    public const int MaxActivity = 100;

    public const int MaxExp = 100_000;

    public static int ClampActivity(int activity)
    {
        return Math.Clamp(activity, 0, MaxActivity);
    }

    public static int ClampExp(int experience)
    {
        return Math.Clamp(experience, 0, MaxExp);
    }

    public static int Pack(int activity, int experience)
    {
        return ClampActivity(activity) * ActivityScale + ClampExp(experience);
    }

    public static int Activity(int packed)
    {
        return packed / ActivityScale;
    }

    public static int Exp(int packed)
    {
        return packed % ActivityScale;
    }

    public static int FeedActivity(int currentActivity, int addedActivity)
    {
        return ClampActivity(currentActivity + addedActivity);
    }
}
