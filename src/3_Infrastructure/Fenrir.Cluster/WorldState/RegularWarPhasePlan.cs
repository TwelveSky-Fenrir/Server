namespace Fenrir.Cluster.WorldState;

public static class RegularWarPhasePlan
{
    public const int MaxTransitionsPerAdvance = 64;

    public static TimeSpan DurationOf(RegularWarStage stage)
    {
        return stage switch
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
    }

    public static RegularWarStage NextOf(RegularWarStage stage)
    {
        return stage switch
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
    }

    public static int PublishedStateOf(RegularWarStage stage)
    {
        return stage switch
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
}
