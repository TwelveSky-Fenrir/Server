namespace Fenrir.Cluster.WorldState;

/// <summary>One tribe's aggregated stat contribution to the 6-beat tribe-score recompute.</summary>
public readonly record struct CenterTribeStatAggregate(byte TribeId, long StatSum);

/// <summary>
///     The character-stats source for the 6-beat tribe-score recompute (Bug 3 fix). DECLARED here; the
///     implementation is owned by Main / the data unit and needs a NEW stored procedure over the character-stats
///     table -- flagged, since the existing procedure set has nothing equivalent.
/// </summary>
/// <remarks>
///     CORRECTS legacy Bug 3: the legacy recompute aggregated against the rank/sync table slot
///     (<c>mDBTable[9]</c>, Server/ts25center/S08_MyDB.cpp:114), which does not carry the character-stat columns
///     the query referenced -- so it would fail on an unknown column (silently in the tick, fatally at boot).
///     The correct source is the character-stats table (<c>mDBTable[2]</c> equivalent, i.e. <c>game.Characters</c>).
///     <para>
///         The required proc (e.g. <c>usp_WorldState_RecomputeTribeScores</c>) must return, per tribe 0..3, the
///         sum over that tribe's characters at level 145 or above of
///         <c>(level1 - 112) + (level2 * 3) + (rebirthCount * 3)</c> -- mapping <c>level1</c>/<c>level2</c>/
///         <c>rebirthCount</c> to the actual <c>game.Characters</c> columns (the data engineer resolves the exact
///         names). The baseline 1000 and the flat +800 for tribe 3 are applied Fenrir-side by
///         <see cref="TribeScoreRecompute" />, not in SQL.
///     </para>
/// </remarks>
public interface ICenterTribeScoreSource
{
    /// <summary>Returns the per-tribe stat-sum aggregates (baseline/flat bonuses NOT yet applied).</summary>
    ValueTask<IReadOnlyList<CenterTribeStatAggregate>> ComputeAsync(CancellationToken ct);
}
