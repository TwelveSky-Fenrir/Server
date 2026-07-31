namespace Fenrir.Cluster.Center.WorldState;

public readonly record struct RegularWarPhaseSnapshot(
    int Index,
    short MapId,
    RegularWarStage Stage,
    int PublishedState,
    DateTimeOffset StageEnteredAt);
