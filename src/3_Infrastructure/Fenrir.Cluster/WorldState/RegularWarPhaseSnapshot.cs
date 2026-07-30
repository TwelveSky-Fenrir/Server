namespace Fenrir.Cluster.WorldState;

public readonly record struct RegularWarPhaseSnapshot(
    int Index,
    short MapId,
    RegularWarStage Stage,
    int PublishedState,
    DateTimeOffset StageEnteredAt);
