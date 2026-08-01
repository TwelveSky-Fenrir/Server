namespace Fenrir.Application.Game.Domain.AntiCheat;

public enum AntiCheatCadence : byte
{
    OneSecond,
    OneSecondProtect,
    ThreeSecond,

    TenSecond,
    ThirtySecond,
    OneMinute,
    OneMinuteHealth,
    OneMinuteSecondary,
    OneMinuteTertiary,
    FiveMinuteHack,
    TenMinute,
    SixtyMinute
}

public struct PlayerCadenceTimers
{
    public TimeSpan OneSecond;
    public TimeSpan OneSecondProtect;
    public TimeSpan ThreeSecond;
    public TimeSpan TenSecond;
    public TimeSpan ThirtySecond;
    public TimeSpan OneMinute;
    public TimeSpan OneMinuteHealth;
    public TimeSpan OneMinuteSecondary;
    public TimeSpan OneMinuteTertiary;
    public TimeSpan FiveMinuteHack;
    public TimeSpan TenMinute;
    public TimeSpan SixtyMinute;

    public void ResetAll(TimeSpan zoneClockNow)
    {
        OneSecond = zoneClockNow;
        OneSecondProtect = zoneClockNow;
        ThreeSecond = zoneClockNow;
        TenSecond = zoneClockNow;
        ThirtySecond = zoneClockNow;
        OneMinute = zoneClockNow;
        OneMinuteHealth = zoneClockNow;
        OneMinuteSecondary = zoneClockNow;
        OneMinuteTertiary = zoneClockNow;
        FiveMinuteHack = zoneClockNow;
        TenMinute = zoneClockNow;
        SixtyMinute = zoneClockNow;
    }

    public static TimeSpan IntervalOf(AntiCheatCadence cadence)
    {
        return cadence switch
        {
            AntiCheatCadence.OneSecond => TimeSpan.FromSeconds(1),
            AntiCheatCadence.OneSecondProtect => TimeSpan.FromSeconds(1),
            AntiCheatCadence.ThreeSecond => TimeSpan.FromSeconds(3),
            AntiCheatCadence.TenSecond => TimeSpan.FromSeconds(10),
            AntiCheatCadence.ThirtySecond => TimeSpan.FromSeconds(30),
            AntiCheatCadence.OneMinute => TimeSpan.FromMinutes(1),
            AntiCheatCadence.OneMinuteHealth => TimeSpan.FromMinutes(1),
            AntiCheatCadence.OneMinuteSecondary => TimeSpan.FromMinutes(1),
            AntiCheatCadence.OneMinuteTertiary => TimeSpan.FromMinutes(1),
            AntiCheatCadence.FiveMinuteHack => TimeSpan.FromMinutes(5),
            AntiCheatCadence.TenMinute => TimeSpan.FromMinutes(10),
            AntiCheatCadence.SixtyMinute => TimeSpan.FromMinutes(60),
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, null)
        };
    }

    public readonly TimeSpan Baseline(AntiCheatCadence cadence)
    {
        return cadence switch
        {
            AntiCheatCadence.OneSecond => OneSecond,
            AntiCheatCadence.OneSecondProtect => OneSecondProtect,
            AntiCheatCadence.ThreeSecond => ThreeSecond,
            AntiCheatCadence.TenSecond => TenSecond,
            AntiCheatCadence.ThirtySecond => ThirtySecond,
            AntiCheatCadence.OneMinute => OneMinute,
            AntiCheatCadence.OneMinuteHealth => OneMinuteHealth,
            AntiCheatCadence.OneMinuteSecondary => OneMinuteSecondary,
            AntiCheatCadence.OneMinuteTertiary => OneMinuteTertiary,
            AntiCheatCadence.FiveMinuteHack => FiveMinuteHack,
            AntiCheatCadence.TenMinute => TenMinute,
            AntiCheatCadence.SixtyMinute => SixtyMinute,
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, null)
        };
    }

    public void Restamp(AntiCheatCadence cadence, TimeSpan zoneClockNow)
    {
        switch (cadence)
        {
            case AntiCheatCadence.OneSecond: OneSecond = zoneClockNow; break;
            case AntiCheatCadence.OneSecondProtect: OneSecondProtect = zoneClockNow; break;
            case AntiCheatCadence.ThreeSecond: ThreeSecond = zoneClockNow; break;
            case AntiCheatCadence.TenSecond: TenSecond = zoneClockNow; break;
            case AntiCheatCadence.ThirtySecond: ThirtySecond = zoneClockNow; break;
            case AntiCheatCadence.OneMinute: OneMinute = zoneClockNow; break;
            case AntiCheatCadence.OneMinuteHealth: OneMinuteHealth = zoneClockNow; break;
            case AntiCheatCadence.OneMinuteSecondary: OneMinuteSecondary = zoneClockNow; break;
            case AntiCheatCadence.OneMinuteTertiary: OneMinuteTertiary = zoneClockNow; break;
            case AntiCheatCadence.FiveMinuteHack: FiveMinuteHack = zoneClockNow; break;
            case AntiCheatCadence.TenMinute: TenMinute = zoneClockNow; break;
            case AntiCheatCadence.SixtyMinute: SixtyMinute = zoneClockNow; break;
            default: throw new ArgumentOutOfRangeException(nameof(cadence), cadence, null);
        }
    }

    public readonly bool HasElapsed(AntiCheatCadence cadence, TimeSpan zoneClockNow)
    {
        return zoneClockNow - Baseline(cadence) >= IntervalOf(cadence);
    }
}
