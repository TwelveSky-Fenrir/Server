using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     Process-wide, thread-safe home for RvR world state -- the in-process merge of the legacy ts25center hub's
///     mWorldInfo/mTribeInfo singleton (every legacy ts25zone process reached the one ts25center over
///     ZCP_ZONE_BROADCAST_FOR_CENTER_SEND; Fenrir runs every zone actor in one process, so they all reach this
///     one instance directly instead). Boot-loads from game.WorldState/WorldStateTribes/WorldStateAllianceOffers
///     via <see cref="InitializeAsync" />, mutated by any zone's tick or handler through the methods below, and
///     flushed back by a periodic write-behind host -- SQL is a durability journal, never read again once loaded.
///     <para>
///         Only models the slice of WORLD_INFO/TRIBE_INFO the current schema persists: tribe symbol ownership,
///         Zone038 ownership, tribe points/gate, and tribe alliance offers. The remaining WorldInfo wire fields
///         (the numbered zone-siege state machines, guild battle, four-guild, etc.) have no backing table yet --
///         they are the responsibility of whichever system builds that feature's own state next to this one.
///     </para>
/// </summary>
public sealed class WorldStateService(IWorldStateRepository repository, ILogger<WorldStateService> logger)
{
    public const int TribeCount = 4;

    private readonly Dictionary<(byte From, byte To), AllianceOfferState> _allianceOffers = new();
    private readonly Lock _lock = new();

    /// <summary>
    ///     Cross-shard convergence accumulator for <see cref="AddTribePoints" />/<see cref="SetTribePoints" />:
    ///     the amount still owed to game.WorldStateTribes.Points via
    ///     <see cref="IWorldStateRepository.AddTribePointsAsync" /> since the last successful
    ///     <see cref="FlushIfDirtyAsync" />. Additive, not a coarse dirty bit, because a delta (unlike an
    ///     absolute overwrite) is not naturally retry-safe -- a failed flush must restore the exact amount,
    ///     not just re-mark "something changed".
    /// </summary>
    private readonly int[] _pendingTribePointDeltas = new int[TribeCount];

    private readonly TribeRvrState[] _tribes = new TribeRvrState[TribeCount];

    private bool _dirty;
    private bool _initialized;

    /// <summary>
    ///     Bumped by every mutator EXCEPT <see cref="AddTribePoints" />/<see cref="SetTribePoints" /> (Points
    ///     converges through <see cref="_pendingTribePointDeltas" /> instead, which is always safe to merge).
    ///     <see cref="ReconcileAsync" /> compares this before/after its DB re-read so it never overwrites a
    ///     genuinely concurrent local mutation with a now-stale read.
    /// </summary>
    private int _scalarVersion;

    private WorldRvrState _world;

    /// <summary>True if any mutation happened since the last successful <see cref="FlushIfDirtyAsync" />.</summary>
    public bool IsDirty
    {
        get
        {
            lock (_lock)
            {
                return _dirty;
            }
        }
    }

    /// <summary>Current WorldState singleton snapshot. Throws until <see cref="InitializeAsync" /> has completed.</summary>
    public WorldRvrState World
    {
        get
        {
            EnsureInitialized();
            lock (_lock)
            {
                return _world;
            }
        }
    }

    /// <summary>
    ///     Idempotent-bootstrap-then-load: seeds the DB rows if this is a first boot, then reads them into the
    ///     in-memory cache. Must complete before any zone accepts a connection. One-shot -- a second call throws.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_initialized)
            throw new InvalidOperationException("WorldStateService.InitializeAsync must only be called once, at boot.");

        await repository.EnsureInitializedAsync(ct).ConfigureAwait(false);
        var (row, tribes, allianceOffers) = await repository.GetAsync(ct).ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                "game.WorldState has no singleton row right after EnsureInitializedAsync -- boot-time invariant violated.");

        lock (_lock)
        {
            _world = new WorldRvrState(row.Zone038WinTribe, row.Zone038WinTribeTime, row.TribeSymbolBattle,
                row.MonsterSymbol, row.MonsterSymbolEndTime, row.HighTribe, row.UpdateTribePoint);

            foreach (var tribe in tribes)
                if (tribe.TribeId < TribeCount)
                    _tribes[tribe.TribeId] =
                        new TribeRvrState(tribe.TribeId, tribe.SymbolDate, tribe.HasSymbol, tribe.Points,
                            tribe.IsClosed);

            foreach (var offer in allianceOffers)
                _allianceOffers[(offer.FromTribeId, offer.ToTribeId)] =
                    new AllianceOfferState(offer.FromTribeId, offer.ToTribeId, offer.IsAccepted);

            _initialized = true;
            _dirty = false;
        }

        logger.LogInformation(
            "WorldState loaded: Zone038WinTribe={Zone038WinTribe} TribeSymbolBattle={TribeSymbolBattle} MonsterSymbol={MonsterSymbol} AllianceOffers={AllianceOfferCount}",
            row.Zone038WinTribe, row.TribeSymbolBattle, row.MonsterSymbol, allianceOffers.Count);
    }

    public TribeRvrState GetTribe(byte tribeId)
    {
        EnsureInitialized();
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _tribes[tribeId];
        }
    }

    public IReadOnlyList<TribeRvrState> GetAllTribes()
    {
        EnsureInitialized();
        lock (_lock)
        {
            return [.. _tribes];
        }
    }

    public IReadOnlyList<AllianceOfferState> GetAllianceOffers()
    {
        EnsureInitialized();
        lock (_lock)
        {
            return [.. _allianceOffers.Values];
        }
    }

    public bool TryGetAllianceOffer(byte fromTribeId, byte toTribeId, out AllianceOfferState offer)
    {
        EnsureInitialized();
        lock (_lock)
        {
            return _allianceOffers.TryGetValue((fromTribeId, toTribeId), out offer);
        }
    }

    /// <summary>
    ///     Legacy <c>ReturnAlliance</c> (Header/function.h:2964-2975): the *other* tribe of whichever active
    ///     alliance pair <paramref name="tribeId" /> belongs to, or null if it is in no active pair -- never
    ///     returns <paramref name="tribeId" /> itself (a real ally lookup can never be reflexive, which is
    ///     exactly the fact <see cref="Progression.TowerFriendlyFireGate" />'s own fix rests on). An "active"
    ///     pair is one where either recorded direction between the two tribes carries
    ///     <see cref="AllianceOfferState.IsAccepted" /> -- the propose/accept/reject flow this schema tracks
    ///     writes one directed (from, to) row per <see cref="SetAllianceOffer" /> call, so a real acceptance
    ///     may land on either (A,B) or (B,A) depending on which side's own accept call fired last.
    /// </summary>
    public byte? GetAllyOf(byte tribeId)
    {
        EnsureInitialized();
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            foreach (var offer in _allianceOffers.Values)
            {
                if (!offer.IsAccepted)
                    continue;
                if (offer.FromTribeId == tribeId)
                    return offer.ToTribeId;
                if (offer.ToTribeId == tribeId)
                    return offer.FromTribeId;
            }

            return null;
        }
    }

    // ---- Mutations -- legacy ZCP_ZONE_BROADCAST_FOR_CENTER_SEND tSort 38/40/42/45/46 equivalents ----
    // (S04_MyWork02.cpp:357-472). Everything else the legacy switch touches (Zone049/051/175/241/etc,
    // TribeGuardState, GuildBattle...) has no column in this schema yet -- out of scope here.

    /// <summary>tSort 38 (ZONE_038): records the deciding tribe and moment.</summary>
    public void SetZone038Winner(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _world = _world with { Zone038WinTribe = tribeId, Zone038WinTribeTime = NowAsLegacyHhMm() };
            _dirty = true;
            _scalarVersion++;
        }
    }

    /// <summary>tSort 40: opens the tribe-symbol battle window; every tribe starts back on its own symbol.</summary>
    public void StartTribeSymbolBattle()
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            _world = _world with { TribeSymbolBattle = true };
            for (byte i = 0; i < TribeCount; i++)
                _tribes[i] = _tribes[i] with { TribeId = i, HasSymbol = true, SymbolDate = now };
            _dirty = true;
            _scalarVersion++;
        }

        logger.LogInformation("WorldState: tribe symbol battle window opened at {SymbolBattleStarted:O}", now);
    }

    /// <summary>tSort 45: closes the tribe-symbol battle window. Per-tribe symbol ownership is left as-is.</summary>
    public void EndTribeSymbolBattle()
    {
        lock (_lock)
        {
            _world = _world with { TribeSymbolBattle = false };
            _dirty = true;
            _scalarVersion++;
        }

        logger.LogInformation("WorldState: tribe symbol battle window closed");
    }

    /// <summary>
    ///     tSort 42, tTribeSymbolIndex 0-3: tribe <paramref name="slotTribeId" />'s own symbol slot is contested.
    ///     The schema only records ownership as a bool on the slot's own row (game.WorldStateTribes.HasSymbol),
    ///     not the challenger's identity, so <paramref name="winnerTribeId" /> only decides whether the slot's
    ///     own tribe keeps it (winner == slot) or loses it (winner != slot).
    /// </summary>
    public void ResolveTribeSymbol(byte slotTribeId, byte winnerTribeId)
    {
        ValidateTribeId(slotTribeId);
        ValidateTribeId(winnerTribeId);
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            _tribes[slotTribeId] = _tribes[slotTribeId] with
            {
                TribeId = slotTribeId,
                HasSymbol = winnerTribeId == slotTribeId,
                SymbolDate = now
            };
            _dirty = true;
            _scalarVersion++;
        }

        logger.LogInformation(
            "WorldState: tribe symbol slot {SlotTribeId} resolved -- winner={WinnerTribeId} kept={Kept}",
            slotTribeId, winnerTribeId, winnerTribeId == slotTribeId);
    }

    /// <summary>tSort 42, tTribeSymbolIndex 4: the neutral monster-guarded symbol is won by <paramref name="winnerTribeId" />.</summary>
    public void ResolveMonsterSymbol(byte winnerTribeId)
    {
        ValidateTribeId(winnerTribeId);
        lock (_lock)
        {
            _world = _world with { MonsterSymbol = winnerTribeId, MonsterSymbolEndTime = NowAsLegacyHhMm() };
            _dirty = true;
            _scalarVersion++;
        }

        logger.LogInformation("WorldState: neutral monster symbol resolved -- winner={WinnerTribeId}", winnerTribeId);
    }

    /// <summary>
    ///     Absolute set (e.g. an admin/reset tool). Prefer <see cref="AddTribePoints" /> for RvR kill-scoring.
    ///     Unlike every other field this class caches, Points converges across shards through the additive
    ///     <see cref="_pendingTribePointDeltas" /> path (<see cref="IWorldStateRepository.AddTribePointsAsync" />),
    ///     not a whole-row overwrite -- so this method locally applies the absolute value (readers of
    ///     <see cref="GetTribe" /> see it immediately) but queues the equivalent DELTA against the
    ///     locally-cached baseline, so the next flush still reaches the DB via the same concurrency-safe
    ///     additive proc instead of silently never persisting (a blind local overwrite here would be
    ///     durably invisible under the new split flush, contradicting this class's own "zero lost updates on
    ///     Points" contract). Does not bump <see cref="_scalarVersion" />: Points is not part of the
    ///     "everything else" reconcile bucket.
    /// </summary>
    public void SetTribePoints(byte tribeId, int points)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            var delta = points - _tribes[tribeId].Points;
            _tribes[tribeId] = _tribes[tribeId] with { TribeId = tribeId, Points = points };
            _pendingTribePointDeltas[tribeId] += delta;
            _dirty = true;
        }
    }

    /// <summary>
    ///     Atomic delta under the same lock every reader uses -- safe for concurrent scoring across zones AND
    ///     across shards: the delta is queued into <see cref="_pendingTribePointDeltas" /> and flushed via
    ///     <see cref="IWorldStateRepository.AddTribePointsAsync" />, additive at the DB, so two shards' deltas
    ///     always sum correctly regardless of interleaving. Returns the new LOCAL total (this shard's own
    ///     up-to-date view; <see cref="ReconcileAsync" /> converges it with any other shard's concurrent
    ///     deltas within one flush interval). Does not bump <see cref="_scalarVersion" />.
    /// </summary>
    public int AddTribePoints(byte tribeId, int delta)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            var updated = _tribes[tribeId].Points + delta;
            _tribes[tribeId] = _tribes[tribeId] with { TribeId = tribeId, Points = updated };
            _pendingTribePointDeltas[tribeId] += delta;
            _dirty = true;
            return updated;
        }
    }

    public void SetTribeClosed(byte tribeId, bool isClosed)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _tribes[tribeId] = _tribes[tribeId] with { TribeId = tribeId, IsClosed = isClosed };
            _dirty = true;
            _scalarVersion++;
        }
    }

    public void SetHighTribe(byte? tribeId)
    {
        if (tribeId is { } id)
            ValidateTribeId(id);

        lock (_lock)
        {
            _world = _world with { HighTribe = tribeId };
            _dirty = true;
            _scalarVersion++;
        }
    }

    /// <summary>
    ///     Raw passthrough of the legacy cross-process flag (ts25playuser writes 1 when it has fresh daily tribe
    ///     points pending; ts25center consumes it -&gt; 2 -&gt; back to 0 next hour). The daily recompute job that
    ///     drove it (ts25playuser S07_MyGame01.cpp:449-478, USE_NEW_CLAN_POINT) is out of scope for this cluster.
    ///     Deferred to the next <see cref="FlushIfDirtyAsync" /> cycle like every other scalar setter here -- see
    ///     <see cref="TryConsumeUpdateTribePointFlagAsync" /> for the one caller that instead needs an immediate,
    ///     success-gated write of this same field.
    /// </summary>
    public void SetUpdateTribePointFlag(short value)
    {
        lock (_lock)
        {
            _world = _world with { UpdateTribePoint = value };
            _dirty = true;
            _scalarVersion++;
        }
    }

    /// <summary>
    ///     tSort=1234 poll's step 1 (S07_MyGame01.cpp:253-266): flips <see cref="WorldRvrState.UpdateTribePoint" />
    ///     from <paramref name="expectedPendingValue" /> to <paramref name="consumedValue" />, persisting the
    ///     change immediately -- NOT deferred to the next <see cref="FlushIfDirtyAsync" /> cycle like every other
    ///     scalar mutator on this class -- because the caller (<see cref="FavoredTribeRankBonusLadderService" />)
    ///     must know, on this same tick, whether the rewrite durably succeeded before it is safe to run the rest
    ///     of the sequence the flag gates (the biased recompute and its broadcast). Every other already-cached
    ///     scalar field is round-tripped through the write unchanged.
    ///     <para>
    ///         A no-op that returns false without touching the repository at all when the flag does not
    ///         currently hold <paramref name="expectedPendingValue" /> -- the contract's documented steady
    ///         state, not a failure. Returns false, with the flag left exactly as read, if the write throws --
    ///         the next poll retries the whole sequence from the start, since the flag was never actually
    ///         changed. Returns true, with the in-memory flag already advanced to
    ///         <paramref name="consumedValue" />, only once the write has actually completed successfully.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25center/S07_MyGame01.cpp:253-266 -- reads the flag, and only on the triggering
    ///     value flips it to consumed FIRST, as its own distinct write, before anything else in the sequence
    ///     runs ; Server/ts25center/S07_MyGame01.cpp:199-267 (<c>MyGame::Logic</c>) -- confirms this whole block
    ///     is unguarded by any build macro.
    /// </remarks>
    public async ValueTask<bool> TryConsumeUpdateTribePointFlagAsync(short expectedPendingValue, short consumedValue,
        CancellationToken ct)
    {
        WorldRvrState snapshot;
        lock (_lock)
        {
            if (_world.UpdateTribePoint != expectedPendingValue)
                return false;
            snapshot = _world;
        }

        try
        {
            await repository.UpdateAsync(snapshot.Zone038WinTribe, snapshot.Zone038WinTribeTime,
                    snapshot.TribeSymbolBattle, snapshot.MonsterSymbol, snapshot.MonsterSymbolEndTime,
                    snapshot.HighTribe, consumedValue, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "WorldState: immediate persist of UpdateTribePoint flag consumption failed -- flag left pending, next poll retries the whole sequence");
            return false;
        }

        lock (_lock)
        {
            if (_world.UpdateTribePoint == expectedPendingValue)
            {
                _world = _world with { UpdateTribePoint = consumedValue };
                _scalarVersion++;
            }
        }

        return true;
    }

    /// <summary>
    ///     tSort=1234 poll's step 2 (S08_MyDB.cpp:138-193): immediately overwrites all <see cref="TribeCount" />
    ///     tribes' Points totals -- a full, absolute overwrite, never the additive
    ///     <see cref="AddTribePoints" />/<see cref="IWorldStateRepository.AddTribePointsAsync" /> path, so this
    ///     method must never also queue a pending delta that a later flush would double-apply on top of the
    ///     value it just wrote directly (unlike <see cref="SetTribePoints" />, which is exactly that deferred/
    ///     additive path and is deliberately NOT reused here). The in-memory mirror is overwritten BEFORE the
    ///     persist is attempted -- matching the contract's documented "mirror changes even on eventual failure"
    ///     edge case -- and is never rolled back if the persist then fails: the caller must treat a false return
    ///     as "this recompute is now permanently lost for this request", not something to retry.
    ///     <para>
    ///         Legacy parity note: the legacy schema writes all 4 totals in one UPDATE statement against a
    ///         single denormalized worldinfo row; this schema normalizes one row per tribe
    ///         (game.WorldStateTribes), so this method issues one <see cref="IWorldStateRepository.UpdateTribeAsync" />
    ///         call per tribe instead of one combined statement -- behaviorally equivalent for this contract (all
    ///         4 succeed, or the whole recompute is treated as failed), not a byte-for-byte reproduction of the
    ///         single-statement shape. Every other column on each tribe's row (SymbolDate/HasSymbol/IsClosed) is
    ///         round-tripped unchanged.
    ///     </para>
    ///     <para>
    ///         Fenrir-only divergence from the narrow "mirror not rolled back" legacy window: unlike the legacy's
    ///         single-process ts25center (which never re-read this row again until next boot), this shard's own
    ///         periodic <see cref="ReconcileAsync" /> unconditionally re-derives each tribe's Points from the DB
    ///         value plus any pending delta on its very next cycle -- for a tribe whose persist failed here, that
    ///         will pull the in-memory mirror back down to the stale DB value rather than leaving it diverged
    ///         indefinitely. This is a deliberate, documented behavior difference introduced by Fenrir's
    ///         cross-shard reconcile mechanism, which has no legacy equivalent -- it is not a bug in either the
    ///         legacy source or this port.
    ///     </para>
    /// </summary>
    public async ValueTask<bool> TryOverwriteTribePointTotalsAsync(IReadOnlyList<int> totals, CancellationToken ct)
    {
        if (totals.Count != TribeCount)
            throw new ArgumentException($"Expected exactly {TribeCount} totals.", nameof(totals));

        TribeRvrState[] updated;
        lock (_lock)
        {
            updated = new TribeRvrState[TribeCount];
            for (byte i = 0; i < TribeCount; i++)
            {
                _tribes[i] = _tribes[i] with { TribeId = i, Points = totals[i] };
                updated[i] = _tribes[i];
            }
        }

        var allSucceeded = true;
        foreach (var tribe in updated)
        {
            try
            {
                await repository.UpdateTribeAsync(tribe.TribeId, tribe.SymbolDate, tribe.HasSymbol, tribe.Points,
                    tribe.IsClosed, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                allSucceeded = false;
                logger.LogError(ex,
                    "WorldState: immediate persist of tribe {TribeId} point total failed -- in-memory mirror already changed and will not be retried for this request",
                    tribe.TribeId);
            }
        }

        return allSucceeded;
    }

    /// <summary>
    ///     tSort 46/47/49: direct upsert of one offer pair. Accept/reject-slot semantics
    ///     (mAllianceState/mPossibleAllianceInfo in the legacy) are not modeled by this schema yet -- see
    ///     game.WorldStateAllianceOffers.sql.
    /// </summary>
    public void SetAllianceOffer(byte fromTribeId, byte toTribeId, bool isAccepted)
    {
        ValidateTribeId(fromTribeId);
        ValidateTribeId(toTribeId);
        if (fromTribeId == toTribeId)
            throw new ArgumentException("An alliance offer cannot target the offering tribe itself.",
                nameof(toTribeId));

        lock (_lock)
        {
            _allianceOffers[(fromTribeId, toTribeId)] = new AllianceOfferState(fromTribeId, toTribeId, isAccepted);
            _dirty = true;
            _scalarVersion++;
        }
    }

    /// <summary>
    ///     Dissolves whichever active alliance pairing currently links <paramref name="tribeA" /> and
    ///     <paramref name="tribeB" />, if any -- flips whichever directed (from, to) row is the accepted one
    ///     back to not-accepted, so <see cref="GetAllyOf" /> stops returning it for either tribe. A no-op if
    ///     the two are not currently allied to each other. Used by
    ///     <see cref="ZoneWar.AllianceDiplomacyCeremony" />'s already-allied-negotiation completion
    ///     (S04_MyWork02.cpp:427-448 case 47) and the separate alliance-stone-destruction mechanism.
    /// </summary>
    public void DissolveAlliance(byte tribeA, byte tribeB)
    {
        ValidateTribeId(tribeA);
        ValidateTribeId(tribeB);

        lock (_lock)
        {
            foreach (var key in _allianceOffers.Keys.ToArray())
            {
                var offer = _allianceOffers[key];
                if (!offer.IsAccepted)
                    continue;

                var linksTheseTwoTribes = (offer.FromTribeId == tribeA && offer.ToTribeId == tribeB) ||
                                          (offer.FromTribeId == tribeB && offer.ToTribeId == tribeA);
                if (!linksTheseTwoTribes)
                    continue;

                _allianceOffers[key] = offer with { IsAccepted = false };
                _dirty = true;
                _scalarVersion++;
            }
        }
    }

    /// <summary>TRIBE_WORK tSort 55's tally read -- the Force Leader candidate list, highest VotePoint first.</summary>
    public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        ValidateTribeId(tribeId);
        return repository.GetTribeVotesAsync(tribeId, ct);
    }

    /// <summary>
    ///     TRIBE_WORK tSort 1/57 (TRIBE_VOTE_V2 branch): registers a Force Leader candidate into
    ///     <paramref name="slotIndex" />, displacing whoever is there today. Straight passthrough -- unlike
    ///     the scalar WorldState/Tribe fields above, candidate slots are never cached in-process (the election
    ///     is a low-frequency, DB-of-record feature, not a per-tick read), so there is nothing to mark dirty
    ///     here.
    /// </summary>
    public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct)
    {
        ValidateTribeId(tribeId);
        return repository.RegisterTribeVoteCandidateAsync(tribeId, slotIndex, candidateCharacterId, candidateLevel,
            killOtherTribeCount, ct);
    }

    /// <summary>TRIBE_WORK tSort 3/59 (vote): adds one voter's computed points onto their chosen candidate slot.</summary>
    public ValueTask CastTribeVoteAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct)
    {
        ValidateTribeId(tribeId);
        return repository.AddTribeVotePointsAsync(tribeId, slotIndex, points, ct);
    }

    /// <summary>TRIBE_WORK tSort 52/56: wipes every candidate slot for one tribe ahead of a fresh election cycle.</summary>
    public ValueTask ResetTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        ValidateTribeId(tribeId);
        return repository.ClearTribeVotesAsync(tribeId, ct);
    }

    /// <summary>
    ///     Persists the whole cache if anything changed since the last flush; no-ops otherwise. Mirrors the
    ///     legacy ts25center tick (S07_MyGame01.cpp:241-267), which unconditionally re-wrote the whole
    ///     worldinfo/tribeinfo row every 6 ticks -- this only skips the round trip when nothing actually
    ///     changed. Never throws: a failed flush is logged and left dirty for the next interval to retry.
    ///     <para>
    ///         Split in two independent halves so a Points-delta failure can never re-dirty the (usually
    ///         unrelated) scalar/symbol-state/alliance-offer writes, and vice versa: the world/symbol-state/
    ///         offer half persists the always-single-writer-by-construction fields exactly like before
    ///         (<see cref="IWorldStateRepository.UpdateAsync" />/<see cref="IWorldStateRepository.UpdateTribeSymbolStateAsync" />/
    ///         <see cref="IWorldStateRepository.SetAllianceOfferAsync" />); the Points-delta half calls
    ///         <see cref="IWorldStateRepository.AddTribePointsAsync" /> only for tribes with a nonzero pending
    ///         delta, additive at the DB so concurrent shards' deltas can never clobber each other.
    ///     </para>
    /// </summary>
    public async ValueTask FlushIfDirtyAsync(CancellationToken ct)
    {
        WorldRvrState world;
        TribeRvrState[] tribes;
        AllianceOfferState[] allianceOffers;
        int[] pointDeltaSnapshot;

        lock (_lock)
        {
            if (!_dirty)
                return;

            world = _world;
            tribes = (TribeRvrState[])_tribes.Clone();
            allianceOffers = [.. _allianceOffers.Values];

            // Zeroed optimistically -- any call below that fails restores its own share of this snapshot
            // (see the two catch blocks), never leaving a partial amount silently unaccounted for.
            pointDeltaSnapshot = (int[])_pendingTribePointDeltas.Clone();
            Array.Clear(_pendingTribePointDeltas);
        }

        try
        {
            await repository.UpdateAsync(world.Zone038WinTribe, world.Zone038WinTribeTime, world.TribeSymbolBattle,
                    world.MonsterSymbol, world.MonsterSymbolEndTime, world.HighTribe, world.UpdateTribePoint, ct)
                .ConfigureAwait(false);

            foreach (var tribe in tribes)
                await repository.UpdateTribeSymbolStateAsync(tribe.TribeId, tribe.SymbolDate, tribe.HasSymbol,
                    tribe.IsClosed, ct).ConfigureAwait(false);

            foreach (var offer in allianceOffers)
                await repository.SetAllianceOfferAsync(offer.FromTribeId, offer.ToTribeId, offer.IsAccepted, ct)
                    .ConfigureAwait(false);

            lock (_lock)
            {
                _dirty = false;
            }
        }
        catch (Exception ex)
        {
            // Compensate: the whole snapshot goes back, since none of it is confirmed persisted.
            lock (_lock)
            {
                for (byte i = 0; i < TribeCount; i++)
                    _pendingTribePointDeltas[i] += pointDeltaSnapshot[i];
            }

            logger.LogError(ex, "WorldState flush failed -- will retry next interval");
            return;
        }

        for (byte i = 0; i < TribeCount; i++)
        {
            if (pointDeltaSnapshot[i] == 0)
                continue;

            try
            {
                await repository.AddTribePointsAsync(i, pointDeltaSnapshot[i], ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Compensate this tribe's own share only -- restore the exact amount (not a coarse dirty
                // bit) and re-arm _dirty so the next interval's early-return doesn't skip retrying it.
                lock (_lock)
                {
                    _pendingTribePointDeltas[i] += pointDeltaSnapshot[i];
                    _dirty = true;
                }

                logger.LogError(ex,
                    "WorldState tribe {TribeId} point-delta flush failed -- delta re-queued for next interval", i);
            }
        }
    }

    /// <summary>
    ///     Cross-shard convergence: re-reads game.WorldState/WorldStateTribes/WorldStateAllianceOffers and
    ///     merges the result into the in-process cache without stomping this shard's own not-yet-flushed
    ///     local mutations. Called every <c>WorldStateWriteBehindHost</c> cycle, immediately after
    ///     <see cref="FlushIfDirtyAsync" />, so a competing shard's just-flushed change (including this
    ///     shard's own delta, already summed into game.WorldStateTribes.Points by
    ///     <see cref="IWorldStateRepository.AddTribePointsAsync" />) becomes visible here within one flush
    ///     interval. Never throws -- a failed reconcile is logged and simply retried next interval, same
    ///     posture as <see cref="FlushIfDirtyAsync" />.
    ///     <para>
    ///         Points always merges safely: DB total + whatever this shard has queued locally since the read
    ///         started (<see cref="_pendingTribePointDeltas" />) -- a plain sum, never a stomp, so it needs no
    ///         dirty-flag guard. Every other field only swaps in the fresh DB value if <see cref="_scalarVersion" />
    ///         is unchanged from just before the read; otherwise this cycle is skipped for those fields and the
    ///         next cycle retries, so a genuinely concurrent local mutation can never be clobbered by a now-stale
    ///         read.
    ///     </para>
    /// </summary>
    public async ValueTask ReconcileAsync(CancellationToken ct)
    {
        if (!_initialized)
            return;

        int scalarVersionBeforeRead;
        lock (_lock)
        {
            scalarVersionBeforeRead = _scalarVersion;
        }

        WorldStateRowDto? row;
        ReadOnlyCollection<WorldStateTribeDto> tribes;
        ReadOnlyCollection<WorldStateAllianceOfferDto> allianceOffers;

        try
        {
            (row, tribes, allianceOffers) = await repository.GetAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WorldState reconcile read failed -- will retry next interval");
            return;
        }

        if (row is null)
        {
            logger.LogError(
                "WorldState reconcile: game.WorldState has no singleton row -- skipping this cycle (boot-time invariant should have prevented this)");
            return;
        }

        lock (_lock)
        {
            var scalarUnchangedSinceRead = _scalarVersion == scalarVersionBeforeRead;

            foreach (var tribe in tribes)
            {
                if (tribe.TribeId >= TribeCount)
                    continue;

                var mergedPoints = tribe.Points + _pendingTribePointDeltas[tribe.TribeId];
                _tribes[tribe.TribeId] = scalarUnchangedSinceRead
                    ? _tribes[tribe.TribeId] with
                    {
                        TribeId = tribe.TribeId,
                        SymbolDate = tribe.SymbolDate,
                        HasSymbol = tribe.HasSymbol,
                        IsClosed = tribe.IsClosed,
                        Points = mergedPoints
                    }
                    : _tribes[tribe.TribeId] with { Points = mergedPoints };
            }

            if (scalarUnchangedSinceRead)
            {
                _world = _world with
                {
                    Zone038WinTribe = row.Zone038WinTribe,
                    Zone038WinTribeTime = row.Zone038WinTribeTime,
                    TribeSymbolBattle = row.TribeSymbolBattle,
                    MonsterSymbol = row.MonsterSymbol,
                    MonsterSymbolEndTime = row.MonsterSymbolEndTime,
                    HighTribe = row.HighTribe,
                    UpdateTribePoint = row.UpdateTribePoint
                };

                _allianceOffers.Clear();
                foreach (var offer in allianceOffers)
                    _allianceOffers[(offer.FromTribeId, offer.ToTribeId)] =
                        new AllianceOfferState(offer.FromTribeId, offer.ToTribeId, offer.IsAccepted);
            }
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "WorldStateService is not loaded yet -- call InitializeAsync before accepting connections.");
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }

    /// <summary>
    ///     Mirrors the legacy's <c>ReturnNowTime()</c> (datetime.h:85-90): hour*100+minute, display-only --
    ///     ts25zone comments confirm every reader of this field just shows it, never does elapsed-time math
    ///     against it (S07_MyGame08.cpp:205,292 "use realtime").
    /// </summary>
    private static int NowAsLegacyHhMm()
    {
        var now = DateTime.UtcNow;
        return now.Hour * 100 + now.Minute;
    }
}
