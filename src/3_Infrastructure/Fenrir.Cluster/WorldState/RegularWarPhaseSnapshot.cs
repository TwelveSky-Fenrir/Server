namespace Fenrir.Cluster.WorldState;

/// <summary>
/// Immutable point-in-time view of one Regular War instance's phase, for observability, tests, and the
/// persistence/preload seam owned by the <c>worldstate-aggregates</c> unit.
/// </summary>
/// <param name="Index">The RW instance index, <c>0..10</c>.</param>
/// <param name="MapId">The map (legacy server number) hosting this instance.</param>
/// <param name="Stage">The internal lifecycle stage currently running.</param>
/// <param name="PublishedState">The discrete phase value most recently published to shards (<c>0..5</c>).</param>
/// <param name="StageEnteredAt">The absolute (monotonic-clock) instant the current <see cref="Stage"/> began.
/// This is the durable anchor a correct restart must preserve — legacy overwrote its equivalent
/// (<c>mZone049TypeStateTime</c>) with a boot-time HHMM stamp, losing the true phase age; Fenrir keeps an
/// absolute instant instead.</param>
public readonly record struct RegularWarPhaseSnapshot(
    int Index,
    short MapId,
    RegularWarStage Stage,
    int PublishedState,
    DateTimeOffset StageEnteredAt);
