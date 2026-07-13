namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The CenterServer's single authoritative writer of hero-rank point accrual. Consumed by the op33
///     ingester's <c>tSort == 9998</c> path (a separate unit). Owned and implemented by the
///     <c>worldstate-aggregates</c> unit (<see cref="HeroRankAuthority" />).
/// </summary>
/// <remarks>
///     Reimplemented from <c>AddOrUpdateHeroRank</c> (Server/ts25center/S08_MyDB.cpp:213-216). CORRECTS legacy
///     Bug 1 (frozen hLevel): the accumulated point total is authoritative and the reported level is tracked
///     monotonically (never regresses), rather than being frozen at the character's level on the first gain of
///     the cycle. Point accrual (hPoint += delta) stays.
/// </remarks>
public interface IHeroRankAuthority
{
    /// <summary>Loads the current-period per-character accrual snapshot from the DB. Refuses on failure.</summary>
    Task InitializeAsync(CancellationToken ct);

    /// <summary>
    ///     Records one hero-point gain reported by a zone: accumulates <paramref name="hPoint" /> onto the
    ///     character's current-period total, refreshes the tribe, and tracks <paramref name="hLevel" />
    ///     monotonically. In-memory, single-writer, non-blocking; the running total is flushed by the ~6s
    ///     cadence host.
    /// </summary>
    void AddOrUpdate(int hPoint, byte hTribe, int hLevel, int uCharIdx);

    /// <summary>Flushes every character whose accrual changed since the last flush. Idempotent (wholesale upsert).</summary>
    ValueTask FlushDirtyAsync(CancellationToken ct);
}
