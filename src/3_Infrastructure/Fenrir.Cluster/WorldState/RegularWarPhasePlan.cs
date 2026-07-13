namespace Fenrir.Cluster.WorldState;

/// <summary>
/// The Regular War (Zone049) phase schedule: for each <see cref="RegularWarStage"/>, its wall-clock
/// duration, the stage it advances into, and the discrete phase value it publishes to shards. This is the
/// single source of truth for the RW cadence.
/// </summary>
/// <remarks>
/// All durations are real wall-clock <see cref="TimeSpan"/>s, NEVER a tick count. The legacy shard measured
/// these with a 2 Hz tick counter (<c>mZone049TypePostTick</c> vs. <c>GetGameTickMinute = 120 × minutes</c>);
/// the contract explicitly requires wall-clock timers instead so any host stall/pause cannot skew the RW
/// schedule. The full cycle is a linear ring:
/// <code>
/// Cooldown(30m) -> AnnounceCountdown(10m) -> PreOpenConfirmation(1m)
///   -> EntryWindow(3m,pub 1) -> Gathering(1m,pub 2) -> WarActive(15m,pub 3)
///   -> ReturnToTown(1.5m,pub 4) -> Closing(6s,pub 5) -> Cooldown ...
/// </code>
/// Published-value band: Cooldown/AnnounceCountdown/PreOpenConfirmation all publish <c>0</c>; the value only
/// changes at EntryWindow(1)/Gathering(2)/WarActive(3)/ReturnToTown(4)/Closing(5).
/// <para>
/// The <c>WarActive -&gt; ReturnToTown</c> edge here always advances into the after-war window (published
/// <c>4</c>). Legacy split this into null/victory (state 4) vs. abandon (state 5, no valid players remaining)
/// based on shard-side PvP scoring and live population — neither of which the Center owns this lot. The
/// winner-tribe outcome and the abandon short-circuit are deferred hooks (Lot 5, when the shard feeds the
/// Center population/scoring); the phase timing itself is complete and authoritative.
/// </para>
/// </remarks>
public static class RegularWarPhasePlan
{
    /// <summary>Upper bound on stage transitions applied in a single <c>Advance</c> call, so a badly stalled
    /// host cannot spin unboundedly while catching up. One full cycle is 8 transitions; 64 tolerates several
    /// missed cycles and still terminates.</summary>
    public const int MaxTransitionsPerAdvance = 64;

    /// <summary>The wall-clock duration of <paramref name="stage"/>.</summary>
    public static TimeSpan DurationOf(RegularWarStage stage) => stage switch
    {
        RegularWarStage.Cooldown => TimeSpan.FromMinutes(30),
        RegularWarStage.AnnounceCountdown => TimeSpan.FromMinutes(10),
        RegularWarStage.PreOpenConfirmation => TimeSpan.FromMinutes(1),
        RegularWarStage.EntryWindow => TimeSpan.FromMinutes(3),
        RegularWarStage.Gathering => TimeSpan.FromMinutes(1),
        RegularWarStage.WarActive => TimeSpan.FromMinutes(15),
        RegularWarStage.ReturnToTown => TimeSpan.FromSeconds(90),
        RegularWarStage.Closing => TimeSpan.FromSeconds(6),
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown Regular War stage.")
    };

    /// <summary>The stage that <paramref name="stage"/> advances into once its duration elapses.</summary>
    public static RegularWarStage NextOf(RegularWarStage stage) => stage switch
    {
        RegularWarStage.Cooldown => RegularWarStage.AnnounceCountdown,
        RegularWarStage.AnnounceCountdown => RegularWarStage.PreOpenConfirmation,
        RegularWarStage.PreOpenConfirmation => RegularWarStage.EntryWindow,
        RegularWarStage.EntryWindow => RegularWarStage.Gathering,
        RegularWarStage.Gathering => RegularWarStage.WarActive,
        RegularWarStage.WarActive => RegularWarStage.ReturnToTown,
        RegularWarStage.ReturnToTown => RegularWarStage.Closing,
        RegularWarStage.Closing => RegularWarStage.Cooldown,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown Regular War stage.")
    };

    /// <summary>The discrete <c>mZone049TypeState</c> value that <paramref name="stage"/> publishes to shards.</summary>
    public static int PublishedStateOf(RegularWarStage stage) => stage switch
    {
        RegularWarStage.Cooldown => 0,
        RegularWarStage.AnnounceCountdown => 0,
        RegularWarStage.PreOpenConfirmation => 0,
        RegularWarStage.EntryWindow => 1,
        RegularWarStage.Gathering => 2,
        RegularWarStage.WarActive => 3,
        RegularWarStage.ReturnToTown => 4,
        RegularWarStage.Closing => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown Regular War stage.")
    };
}
