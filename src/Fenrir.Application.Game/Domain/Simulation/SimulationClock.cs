namespace Fenrir.Application.Game.Domain.Simulation;

public static class SimulationClock
{
    public const int LegacyTickMilliseconds = 499;

    public const int MonsterRespawnScanLegacyTicks = 20;

    public const int MonsterDetectionThrottleLegacyTicks = 2;

    public const int PetActivityDecayLegacyTicks = 60;

    public const int MonsterIdleReturnHomeLegacyTicks = 120;

    public const int MonsterIdleWanderLegacyTicks = 80;

    public const int PlayTimeAccrualLegacyTicks = 120;

    public const int OneSecondGateLegacyTicks = 2;

    public const int ReviveEligibilityLegacyTicks = 10;

    public const int DeathBroadcastSuppressionLegacyTicks = 30;

    public const int AntiAbuseForceQuitLegacyTicks = 50;

    public static readonly TimeSpan LegacyTick = TimeSpan.FromMilliseconds(LegacyTickMilliseconds);

    public static readonly TimeSpan DarkAttackPotionDebuffDuration = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan AvatarRebroadcastInterval = TimeSpan.FromSeconds(3.5);

    public static readonly TimeSpan MonsterRebroadcastInterval = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan GroundItemRebroadcastInterval = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan ProxyShopRebroadcastInterval = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan ReviveEligibilityDelay = ToTimeSpan(ReviveEligibilityLegacyTicks);

    public static readonly TimeSpan GroundItemLifetime = TimeSpan.FromMilliseconds(60_000);

    public static readonly TimeSpan GroundItemFreeForAllDelay = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan GroundItemPartyShareDelay = TimeSpan.FromSeconds(10);

    public static TimeSpan ToTimeSpan(int legacyTicks)
    {
        return legacyTicks * LegacyTick;
    }

    public static TimeSpan RebroadcastStaggerOffset(int entityId, TimeSpan interval)
    {
        var bucketCount = (int)(interval.Ticks / LegacyTick.Ticks);
        if (bucketCount <= 0)
            return TimeSpan.Zero;

        var bucket = (entityId % bucketCount + bucketCount) % bucketCount;
        return TimeSpan.FromTicks(interval.Ticks * bucket / bucketCount);
    }

    public static int DetectionThrottleStaggerOffsetTicks(int entityId)
    {
        var bucketCount = MonsterDetectionThrottleLegacyTicks;
        if (bucketCount <= 0)
            return 0;

        return (entityId % bucketCount + bucketCount) % bucketCount;
    }

    public static int ToWholeLegacyTicks(TimeSpan duration)
    {
        return duration <= TimeSpan.Zero ? 0 : (int)(duration.Ticks / LegacyTick.Ticks);
    }
}
