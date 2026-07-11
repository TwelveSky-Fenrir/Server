namespace Fenrir.Application.Game.Domain.AntiCheat;

/// <summary>
///     The twelve named "cadence" anti-cheat timers legacy declares per session
///     (<c>Server/ts25zone/H07_MyGame.h:821-832</c>: <c>01Second</c>, <c>01SecondForProtect</c>, <c>30Second</c>,
///     <c>01MinuteForHealth</c>, <c>01Minute</c>/<c>_2</c>/<c>_3</c>, <c>03Second</c>, <c>10Second</c>,
///     <c>10Minute</c>, <c>Hack5Minute</c>, <c>60Minute</c>) and re-baselines to the current tick at avatar
///     registration (<c>Server/ts25zone/S04_MyWork02.cpp:837-847</c>). The interval each name encodes (1s, 3s,
///     10s, ...) is derived directly from the field name — it is NOT an invented threshold.
/// </summary>
/// <remarks>
///     Deliberately kept as a pure timer group with NO wired consumer: the contract (D2 anti-cheat) proves
///     only that these baselines exist and are reset on entry.
///     <b>
///         The algorithm that consumes each cadence
///         (what actually happens once an interval elapses) is not present in any cited legacy line
///     </b>
///     , so it is
///     intentionally unmodeled here — do not invent a per-cadence action. <see cref="HasElapsed" /> is provided
///     as neutral infrastructure for whoever later cites and models a specific cadence's consumption; until
///     then nothing in Fenrir reads these except the reset-on-entry invariant tested in
///     <c>PlayerAntiCheatResetTests</c>.
/// </remarks>
public enum AntiCheatCadence : byte
{
    OneSecond,
    OneSecondProtect,
    ThreeSecond,

    /// <summary>
    ///     Legacy <c>10Second</c> — declared but NOT in the registration reset block; Fenrir resets it anyway (see
    ///     <see cref="PlayerCadenceTimers.ResetAll" />).
    /// </summary>
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

/// <summary>
///     Value-type (zero-heap, inline in <c>PlayerRuntimeState</c>) holder of the twelve <see cref="AntiCheatCadence" />
///     baselines, each a zone-clock instant (<c>TimeSpan</c>), matching every other zone-clock stamp on
///     <c>PlayerRuntimeState</c> (<c>ZoneEntryAtZoneClock</c>, <c>LastAvatarRebroadcastAt</c>). Mutated only by
///     the owning character's own zone tick thread — the single-writer contract the whole runtime-state type
///     relies on.
/// </summary>
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

    /// <summary>
    ///     Re-baselines every cadence to <paramref name="zoneClockNow" /> — the "clean slate on entry" property
    ///     from side effect #1 of the D2 contract. Fenrir deliberately resets ALL twelve, including
    ///     <see cref="AntiCheatCadence.TenSecond" /> which legacy leaves stale at registration
    ///     (<c>H07_MyGame.h:829</c> is absent from the <c>S04_MyWork02.cpp:837-847</c> reset block): a stale
    ///     cross-session anti-cheat baseline is itself a (minor) weakness, so the hardened choice is a full
    ///     reset rather than reproducing legacy's partial one.
    /// </summary>
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

    /// <summary>The interval each cadence name encodes — derived from the legacy field name, not invented.</summary>
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

    /// <summary>The zone-clock instant this cadence was last re-baselined.</summary>
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

    /// <summary>
    ///     Re-baselines a single cadence to <paramref name="zoneClockNow" /> (the "fire and rearm" half of a consumer,
    ///     once one is modeled).
    /// </summary>
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

    /// <summary>
    ///     True once <see cref="IntervalOf" /> has elapsed since this cadence's <see cref="Baseline" />. Neutral
    ///     infrastructure only — <b>what to do</b> when a cadence elapses is unmodeled (no cited consumer), so
    ///     nothing in Fenrir currently calls this.
    /// </summary>
    public readonly bool HasElapsed(AntiCheatCadence cadence, TimeSpan zoneClockNow)
    {
        return zoneClockNow - Baseline(cadence) >= IntervalOf(cadence);
    }
}
