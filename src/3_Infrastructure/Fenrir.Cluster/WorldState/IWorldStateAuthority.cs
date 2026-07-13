namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The CenterServer's single authoritative writer of cross-zone RvR world state. Mirrors the shard-side
///     <c>WorldStateService</c> but is the ONE writer for the whole cluster (the contract's "ecrivain UNIQUE").
///     Consumed by the op33 world-event ingester and by <see cref="RegularWarPhaseAuthority" /> (both separate
///     units), which map recognised events to the semantic routing calls below.
/// </summary>
/// <remarks>
///     OWNERSHIP: the <c>worldstate-aggregates</c> unit owns this interface and its implementation
///     (<see cref="WorldStateAuthority" />). This is the full superset; the <c>regularwar-zone049-phase</c>
///     unit shipped a one-member stub of the same type declaring only <see cref="SetZone049State" /> so it
///     could compile in parallel -- this file supersedes it, keeping that member's exact signature.
///     <para>
///         Reimplemented from the CenterServer op33 dispatch catalogue
///         (Server/ts25center/S04_MyWork02.cpp:212-1213) as a semantic surface rather than a per-tSort switch:
///         the ingester owns the tSort-&gt;method mapping, this authority owns the aggregate storage,
///         index-bounds validation, and real-time timestamping.
///     </para>
///     <para>
///         Every indexed setter returns whether the write was applied: <c>false</c> means the supplied index
///         was out of the target array's bounds and the write was dropped (legacy performed an unchecked SHM
///         out-of-bounds write here -- the bounds check is the deliberate hardening; the ingester audits on
///         <c>false</c>). Non-indexed setters cannot be rejected and return <c>void</c>.
///     </para>
///     <para>
///         This lot is DORMANT: the machinery runs on Center while shards still author world state (the
///         shard-&gt;center authority flip is Lot 5's atomic gate). Nothing shard-side is removed.
///     </para>
/// </remarks>
public interface IWorldStateAuthority
{
    /// <summary>Loads the persisted aggregate from the DB (seed-if-absent then select). Refuses on failure.</summary>
    Task InitializeAsync(CancellationToken ct);

    /// <summary>Flushes the persisted subset (world row, tribe rows, alliance offers) to the DB if dirty.</summary>
    ValueTask FlushIfDirtyAsync(CancellationToken ct);

    /// <summary>
    ///     Publishes the discrete Regular War (Zone049) phase value for a single RW instance. Convenience alias
    ///     over the Zone049 type-state array for <see cref="RegularWarPhaseAuthority" /> (no time stamp); an
    ///     out-of-range index is dropped and logged rather than writing out of bounds.
    /// </summary>
    /// <param name="index">The RW instance index (0..10); pre-validated by the caller.</param>
    /// <param name="state">The published phase value (0..5); shards read <c>3</c> as "war active".</param>
    void SetZone049State(int index, int state);

    // --- RvR zone type-state (1D indexed arrays; scalar zones use index 0) ---
    bool SetZoneTypeState(WorldZoneStateKind zone, int index, byte state, bool stampTime);

    /// <summary>Zone175 uses a 2D [i1][i2] matrix.</summary>
    bool SetZone175TypeState(int i1, int i2, byte state);

    /// <summary>Tribe-guard matrix [fromTribe][toTribe]. Both indices are tribe ids (0..3).</summary>
    bool SetTribeGuardState(int fromTribe, int toTribe, byte state);

    // --- Symbol battle (Zone038) ---
    bool SetSymbolWinTribe(byte tribe);

    void StartSymbolBattle();

    bool SetTribeSymbol(int symbolIndex, byte winnerTribe);

    bool SetMonsterSymbol(byte winnerTribe);

    void EndSymbolBattle();

    // --- Alliance ---
    bool SetAllianceState(byte tribe1, byte tribe2);

    bool SetPossibleAlliance(byte tribe1, int date1, byte tribe2, int date2);

    // --- Tribe vote (SHM scalar state; candidate rows persist through the tribe-vote repository) ---
    bool SetTribeVoteState(int tribe, byte state);

    ValueTask RegisterTribeVoteCandidateAsync(byte tribe, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct);

    ValueTask ClearTribeVoteCandidateAsync(byte tribe, byte slotIndex, CancellationToken ct);

    ValueTask AddTribeVotePointsAsync(byte tribe, byte slotIndex, int points, CancellationToken ct);

    ValueTask PurgeTribeVoteCandidatesAsync(byte tribe, CancellationToken ct);

    bool SetTribeSubMaster(byte tribe, byte slotIndex, string name);

    bool ClearTribeSubMaster(byte tribe, byte slotIndex);

    // --- Zone194 tribe ratio bonuses (case 301) + master call ability (case 302) ---
    bool SetTribeRatioBonus(TribeRatioBonusKind kind, byte tribe, int rawValue);

    /// <summary>The "kill other tribe add value" family (301 sub 51-54): sets one tribe, zeroes the rest.</summary>
    bool SetTribeKillOtherTribeAddValueExclusive(byte tribe, int value);

    bool SetTribeMasterCallAbility(byte tribe, int skillSort);

    // --- DTM (case 1510) ---
    bool SetTribeDtmValue(byte tribe, int dtm);

    // --- Manual operator-driven tribe-point recompute trigger ---
    void SetUpdateTribePointFlag(short value);

    /// <summary>The four current tribe scores (mTribePoint[0..3]), for the 1234 sync broadcast payload.</summary>
    int[] SnapshotTribePoints();

    /// <summary>The current advantaged tribe (mHighTribe), or null if none. Used by the weekly rotation.</summary>
    byte? HighTribe { get; }

    /// <summary>
    ///     Overwrites all four tribe scores from an authoritative recompute (Bug 3 population recompute, or the
    ///     Bug 2 advantaged-tribe distribution) and marks the aggregate dirty for the next flush.
    /// </summary>
    void OverwriteTribePoints(ReadOnlySpan<int> totals);

    /// <summary>Sets the advantaged tribe index (Bug 2 weekly rotation) and marks dirty.</summary>
    void SetHighTribe(byte? tribeId);

    /// <summary>
    ///     Reads and clears the pending manual-recompute flag: returns <c>true</c> exactly once after an
    ///     operator set the flag to its pending value, advancing it to the consumed value (edge-triggered).
    /// </summary>
    bool TryConsumeUpdateTribePointFlag(short pendingValue, short consumedValue);
}
