using System.Collections.ObjectModel;
using Fenrir.Data.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World.WorldState;

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
    private readonly TribeRvrState[] _tribes = new TribeRvrState[TribeCount];

    private bool _dirty;
    private bool _initialized;
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
        }

        logger.LogInformation("WorldState: neutral monster symbol resolved -- winner={WinnerTribeId}", winnerTribeId);
    }

    /// <summary>Absolute set (e.g. an admin/reset tool). Prefer <see cref="AddTribePoints" /> for RvR kill-scoring.</summary>
    public void SetTribePoints(byte tribeId, int points)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _tribes[tribeId] = _tribes[tribeId] with { TribeId = tribeId, Points = points };
            _dirty = true;
        }
    }

    /// <summary>Atomic delta under the same lock every reader uses -- safe for concurrent scoring across zones. Returns the new total.</summary>
    public int AddTribePoints(byte tribeId, int delta)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            var updated = _tribes[tribeId].Points + delta;
            _tribes[tribeId] = _tribes[tribeId] with { TribeId = tribeId, Points = updated };
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
        }
    }

    /// <summary>
    ///     Raw passthrough of the legacy cross-process flag (ts25playuser writes 1 when it has fresh daily tribe
    ///     points pending; ts25center consumes it -&gt; 2 -&gt; back to 0 next hour). The daily recompute job that
    ///     drove it (ts25playuser S07_MyGame01.cpp:449-478, USE_NEW_CLAN_POINT) is out of scope for this cluster.
    /// </summary>
    public void SetUpdateTribePointFlag(short value)
    {
        lock (_lock)
        {
            _world = _world with { UpdateTribePoint = value };
            _dirty = true;
        }
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
    /// </summary>
    public async ValueTask FlushIfDirtyAsync(CancellationToken ct)
    {
        WorldRvrState world;
        TribeRvrState[] tribes;
        AllianceOfferState[] allianceOffers;

        lock (_lock)
        {
            if (!_dirty)
                return;

            world = _world;
            tribes = (TribeRvrState[])_tribes.Clone();
            allianceOffers = [.. _allianceOffers.Values];
        }

        try
        {
            await repository.UpdateAsync(world.Zone038WinTribe, world.Zone038WinTribeTime, world.TribeSymbolBattle,
                world.MonsterSymbol, world.MonsterSymbolEndTime, world.HighTribe, world.UpdateTribePoint, ct)
                .ConfigureAwait(false);

            foreach (var tribe in tribes)
                await repository.UpdateTribeAsync(tribe.TribeId, tribe.SymbolDate, tribe.HasSymbol, tribe.Points,
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
            logger.LogError(ex, "WorldState flush failed -- will retry next interval");
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
