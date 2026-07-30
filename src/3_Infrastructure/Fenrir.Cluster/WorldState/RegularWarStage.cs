namespace Fenrir.Cluster.WorldState;

public enum RegularWarStage : byte
{
    Cooldown = 0,

    AnnounceCountdown = 1,

    PreOpenConfirmation = 2,

    EntryWindow = 3,

    Gathering = 4,

    WarActive = 5,

    ReturnToTown = 6,

    Closing = 7
}
