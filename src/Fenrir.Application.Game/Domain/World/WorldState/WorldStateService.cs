using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public sealed class WorldStateService(
    IWorldStateRepository repository,
    ILogger<WorldStateService> logger)
{
    public const int TribeCount = 4;

    private readonly Dictionary<(byte From, byte To), AllianceOfferState> _allianceOffers = new();
    private readonly Lock _lock = new();

    private readonly Dictionary<(byte From, byte To), PendingAllianceOfferMutation> _pendingAllianceOffers = new();

    private readonly Dictionary<byte, PendingTribeStateMutation> _pendingTribeStates = new();

    private readonly SemaphoreSlim _persistenceGate = new(1, 1);

    private readonly int[] _pendingTribePointDeltas = new int[TribeCount];

    private readonly int?[] _pendingTribePointTotals = new int?[TribeCount];

    private readonly byte[] _tribeFormationAbility = new byte[TribeCount];

    private readonly TribeRvrState[] _tribes = new TribeRvrState[TribeCount];

    private readonly byte[] _tribeSymbolOwner = new byte[TribeCount];

    private bool _dirty;
    private bool _initialized;

    private long _nextMutationVersion;

    private PendingWorldMutation? _pendingWorld;

    private long _revision;

    private WorldRvrState _world;

    private const int MaxConflictReplayAttempts = 3;

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
                {
                    _tribes[tribe.TribeId] =
                        new TribeRvrState(tribe.TribeId, tribe.SymbolDateUtc, tribe.HasSymbol, tribe.Points,
                            tribe.IsClosed);
                    _tribeSymbolOwner[tribe.TribeId] = tribe.SymbolOwnerTribeId;
                }

            foreach (var offer in allianceOffers)
                _allianceOffers[(offer.FromTribeId, offer.ToTribeId)] =
                    new AllianceOfferState(offer.FromTribeId, offer.ToTribeId, offer.IsAccepted);

            _revision = row.Revision;
            _initialized = true;
            _dirty = false;
        }

        logger.LogInformation(
            "WorldState loaded: Zone038WinTribe={Zone038WinTribe} TribeSymbolBattle={TribeSymbolBattle} MonsterSymbol={MonsterSymbol} AllianceOffers={AllianceOfferCount}",
            row.Zone038WinTribe, row.TribeSymbolBattle, row.MonsterSymbol, allianceOffers.Length);
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

    public byte GetTribeFormationAbility(byte tribeId)
    {
        EnsureInitialized();
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _tribeFormationAbility[tribeId];
        }
    }

    public byte GetTribeSymbolOwner(byte slotTribeId)
    {
        EnsureInitialized();
        ValidateTribeId(slotTribeId);
        lock (_lock)
        {
            return _tribeSymbolOwner[slotTribeId];
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


    public void SetZone038Winner(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _world = _world with { Zone038WinTribe = tribeId, Zone038WinTribeTime = NowAsLegacyHhMm() };
            QueueWorldMutation();
        }
    }

    public void SetTribeFormationAbility(byte tribeId, byte formationCode)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _tribeFormationAbility[tribeId] = formationCode;
        }
    }

    public void StartTribeSymbolBattle()
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            _world = _world with { TribeSymbolBattle = true };
            for (byte i = 0; i < TribeCount; i++)
            {
                _tribes[i] = _tribes[i] with { TribeId = i, HasSymbol = true, SymbolDate = now };
                _tribeSymbolOwner[i] = i;
                QueueTribeStateMutation(i);
            }

            QueueWorldMutation();
        }

        logger.LogInformation("WorldState: tribe symbol battle window opened at {SymbolBattleStarted:O}", now);
    }

    public void EndTribeSymbolBattle()
    {
        lock (_lock)
        {
            _world = _world with { TribeSymbolBattle = false };
            Array.Clear(_tribeFormationAbility);
            QueueWorldMutation();
        }

        logger.LogInformation("WorldState: tribe symbol battle window closed");
    }

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
            _tribeSymbolOwner[slotTribeId] = winnerTribeId;
            QueueTribeStateMutation(slotTribeId);
        }

        logger.LogInformation(
            "WorldState: tribe symbol slot {SlotTribeId} resolved -- winner={WinnerTribeId} kept={Kept}",
            slotTribeId, winnerTribeId, winnerTribeId == slotTribeId);
    }

    public void ResolveMonsterSymbol(byte winnerTribeId)
    {
        ValidateTribeId(winnerTribeId);
        lock (_lock)
        {
            _world = _world with { MonsterSymbol = winnerTribeId, MonsterSymbolEndTime = NowAsLegacyHhMm() };
            QueueWorldMutation();
        }

        logger.LogInformation("WorldState: neutral monster symbol resolved -- winner={WinnerTribeId}", winnerTribeId);
    }

    public void SetTribePoints(byte tribeId, int points)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            var delta = points - _tribes[tribeId].Points;
            _tribes[tribeId] = _tribes[tribeId] with { TribeId = tribeId, Points = points };
            _pendingTribePointDeltas[tribeId] += delta;
            RefreshDirtyLocked();
        }
    }

    public int AddTribePoints(byte tribeId, int delta)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            var updated = _tribes[tribeId].Points + delta;
            _tribes[tribeId] = _tribes[tribeId] with { TribeId = tribeId, Points = updated };
            _pendingTribePointDeltas[tribeId] += delta;
            RefreshDirtyLocked();
            return updated;
        }
    }

    public void SetTribeClosed(byte tribeId, bool isClosed)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _tribes[tribeId] = _tribes[tribeId] with { TribeId = tribeId, IsClosed = isClosed };
            QueueTribeStateMutation(tribeId);
        }
    }

    public void SetHighTribe(byte? tribeId)
    {
        if (tribeId is { } id)
            ValidateTribeId(id);

        lock (_lock)
        {
            _world = _world with { HighTribe = tribeId };
            QueueWorldMutation();
        }
    }

    public void SetUpdateTribePointFlag(short value)
    {
        lock (_lock)
        {
            _world = _world with { UpdateTribePoint = value };
            QueueWorldMutation();
        }
    }

    public async ValueTask<bool> TryConsumeUpdateTribePointFlagAsync(short expectedPendingValue, short consumedValue,
        CancellationToken ct)
    {
        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            WorldRvrState snapshot;
            long expectedRevision;
            PendingWorldMutation? persistedWorldMutation;
            lock (_lock)
            {
                if (_world.UpdateTribePoint != expectedPendingValue)
                    return false;

                snapshot = _world;
                expectedRevision = _revision;
                persistedWorldMutation = _pendingWorld;
            }

            try
            {
                var updated = await repository.TryUpdateAsync(snapshot.Zone038WinTribe, snapshot.Zone038WinTribeTime,
                        snapshot.TribeSymbolBattle, snapshot.MonsterSymbol, snapshot.MonsterSymbolEndTime,
                        snapshot.HighTribe, consumedValue, expectedRevision, ct)
                    .ConfigureAwait(false);

                if (!updated)
                {
                    logger.LogWarning(
                        "WorldState: immediate UpdateTribePoint flag consumption conflicted at revision {ExpectedRevision}; reloading before the next poll",
                        expectedRevision);
                    await ReconcileCoreAsync(ct).ConfigureAwait(false);
                    return false;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "WorldState: immediate persist of UpdateTribePoint flag consumption failed -- flag left pending, next poll retries the whole sequence");
                return false;
            }

            lock (_lock)
            {
                UpdateRevisionAfterSuccessfulWrite(expectedRevision);

                if (_world.UpdateTribePoint == expectedPendingValue)
                {
                    _world = _world with { UpdateTribePoint = consumedValue };
                }

                if (persistedWorldMutation is { } persisted && _pendingWorld is { } pending &&
                    pending.Version == persisted.Version)
                    _pendingWorld = null;
                else if (_pendingWorld is { } currentPending &&
                         currentPending.State.UpdateTribePoint == expectedPendingValue)
                    _pendingWorld = currentPending with
                    {
                        State = currentPending.State with { UpdateTribePoint = consumedValue }
                    };

                RefreshDirtyLocked();
            }

            return true;
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    public async ValueTask<bool> TryOverwriteTribePointTotalsAsync(IReadOnlyList<int> totals, CancellationToken ct)
    {
        if (totals.Count != TribeCount)
            throw new ArgumentException($"Expected exactly {TribeCount} totals.", nameof(totals));

        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            TribeRvrState[] updated;
            var requestedTotals = totals.ToArray();
            long expectedRevision;
            lock (_lock)
            {
                updated = new TribeRvrState[TribeCount];
                for (byte i = 0; i < TribeCount; i++)
                {
                    _tribes[i] = _tribes[i] with { TribeId = i, Points = requestedTotals[i] };
                    updated[i] = _tribes[i];
                }

                Array.Clear(_pendingTribePointDeltas);
                Array.Clear(_pendingTribePointTotals);
                expectedRevision = _revision;
            }

            lock (_lock)
            {
                RefreshDirtyLocked();
            }

            foreach (var tribe in updated)
            {
                bool updatedTribe;
                try
                {
                    updatedTribe = await repository.TryUpdateTribePointsAsync(tribe.TribeId, tribe.Points,
                            expectedRevision, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex,
                        "WorldState: immediate persist of tribe {TribeId} point total failed -- retaining totals for retry",
                        tribe.TribeId);
                    await RetainTribePointTotalsAfterFailedWriteAsync(requestedTotals, ct).ConfigureAwait(false);
                    return false;
                }

                if (!updatedTribe)
                {
                    logger.LogWarning(
                        "WorldState: immediate persist of tribe {TribeId} point total conflicted at revision {ExpectedRevision}; retaining totals for retry",
                        tribe.TribeId, expectedRevision);
                    await RetainTribePointTotalsAfterFailedWriteAsync(requestedTotals, ct).ConfigureAwait(false);
                    return false;
                }

                AdvanceRevisionAfterSuccessfulWrite(ref expectedRevision);
            }

            return true;
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

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
            QueueAllianceOfferMutation(fromTribeId, toTribeId);
        }
    }

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
                QueueAllianceOfferMutation(key.From, key.To);
            }
        }
    }

    public ValueTask<ReadOnlyCollection<TribeVoteDto>> GetTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        ValidateTribeId(tribeId);
        return repository.GetTribeVotesAsync(tribeId, ct);
    }

    public ValueTask RegisterTribeVoteCandidateAsync(byte tribeId, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct)
    {
        ValidateTribeId(tribeId);
        return repository.RegisterTribeVoteCandidateAsync(tribeId, slotIndex, candidateCharacterId, candidateLevel,
            killOtherTribeCount, ct);
    }

    public ValueTask CastTribeVoteAsync(byte tribeId, byte slotIndex, int points, CancellationToken ct)
    {
        ValidateTribeId(tribeId);
        return repository.AddTribeVotePointsAsync(tribeId, slotIndex, points, ct);
    }

    public ValueTask ResetTribeVotesAsync(byte tribeId, CancellationToken ct)
    {
        ValidateTribeId(tribeId);
        return repository.ClearTribeVotesAsync(tribeId, ct);
    }

    public async ValueTask<bool> FlushIfDirtyAsync(CancellationToken ct)
    {
        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var conflictAttempt = 0; ; conflictAttempt++)
            {
                var plan = CaptureFlushPlan();
                if (plan is null)
                    return true;

                var result = await FlushPlanAsync(plan, ct).ConfigureAwait(false);
                if (result == FlushPlanResult.Succeeded)
                    continue;

                if (result == FlushPlanResult.Failed)
                    return false;

                if (!await ReconcileCoreAsync(ct).ConfigureAwait(false))
                    return false;

                if (conflictAttempt == MaxConflictReplayAttempts)
                {
                    logger.LogError(
                        "WorldState flush exhausted {ConflictReplayAttempts} CAS conflict replays; dirty mutations remain queued for the next flush",
                        MaxConflictReplayAttempts + 1);
                    return false;
                }

                await Task.Delay(ConflictReplayBackoff(conflictAttempt), ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    public async ValueTask<bool> ReconcileAsync(CancellationToken ct)
    {
        await _persistenceGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ReconcileCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    private FlushPlan? CaptureFlushPlan()
    {
        lock (_lock)
        {
            if (!_dirty)
                return null;

            var plan = new FlushPlan(
                _revision,
                _pendingWorld,
                [.. _pendingTribeStates.Values.OrderBy(static mutation => mutation.State.TribeId)],
                [.. _pendingAllianceOffers.Values
                    .OrderBy(static mutation => mutation.State.FromTribeId)
                    .ThenBy(static mutation => mutation.State.ToTribeId)],
                (int?[])_pendingTribePointTotals.Clone(),
                (int[])_pendingTribePointDeltas.Clone());

            _pendingWorld = null;
            _pendingTribeStates.Clear();
            _pendingAllianceOffers.Clear();
            Array.Clear(_pendingTribePointTotals);
            Array.Clear(_pendingTribePointDeltas);
            RefreshDirtyLocked();
            return plan;
        }
    }

    private async ValueTask<FlushPlanResult> FlushPlanAsync(FlushPlan plan, CancellationToken ct)
    {
        var expectedRevision = plan.ExpectedRevision;
        var worldCommitted = plan.World is null;
        var firstTribeStateIndex = 0;
        var firstAllianceOfferIndex = 0;
        var firstPointTotalTribeId = 0;
        var firstPointDeltaTribeId = 0;

        try
        {
            if (plan.World is { } world)
            {
                var updated = await repository.TryUpdateAsync(world.State.Zone038WinTribe,
                        world.State.Zone038WinTribeTime, world.State.TribeSymbolBattle, world.State.MonsterSymbol,
                        world.State.MonsterSymbolEndTime, world.State.HighTribe, world.State.UpdateTribePoint,
                        expectedRevision, ct)
                    .ConfigureAwait(false);
                if (!updated)
                    return Conflict(plan, true, 0, 0, 0, 0, "singleton world-state", expectedRevision);

                AdvanceRevisionAfterSuccessfulWrite(ref expectedRevision);
                worldCommitted = true;
            }

            for (var tribeIndex = 0; tribeIndex < plan.TribeStates.Length; tribeIndex++)
            {
                firstTribeStateIndex = tribeIndex;
                var tribe = plan.TribeStates[tribeIndex];
                var updated = await repository.TryUpdateTribeSymbolStateAsync(tribe.State.TribeId,
                        tribe.State.SymbolDate, tribe.State.HasSymbol, tribe.State.IsClosed, tribe.SymbolOwner,
                        expectedRevision, ct)
                    .ConfigureAwait(false);
                if (!updated)
                    return Conflict(plan, false, tribeIndex, 0, 0, 0,
                        $"tribe {tribe.State.TribeId} symbol state", expectedRevision);

                AdvanceRevisionAfterSuccessfulWrite(ref expectedRevision);
                firstTribeStateIndex = tribeIndex + 1;
            }

            for (var offerIndex = 0; offerIndex < plan.AllianceOffers.Length; offerIndex++)
            {
                firstAllianceOfferIndex = offerIndex;
                var offer = plan.AllianceOffers[offerIndex].State;
                var updated = await repository.TrySetAllianceOfferAsync(offer.FromTribeId, offer.ToTribeId,
                        offer.IsAccepted, expectedRevision, ct)
                    .ConfigureAwait(false);
                if (!updated)
                    return Conflict(plan, false, plan.TribeStates.Length, offerIndex, 0, 0,
                        $"alliance offer {offer.FromTribeId}->{offer.ToTribeId}", expectedRevision);

                AdvanceRevisionAfterSuccessfulWrite(ref expectedRevision);
                firstAllianceOfferIndex = offerIndex + 1;
            }

            for (var tribeId = 0; tribeId < TribeCount; tribeId++)
            {
                if (plan.PointTotals[tribeId] is not { } points)
                    continue;

                firstPointTotalTribeId = tribeId;
                var updated = await repository.TryUpdateTribePointsAsync((byte)tribeId, points, expectedRevision, ct)
                    .ConfigureAwait(false);
                if (!updated)
                    return Conflict(plan, false, plan.TribeStates.Length, plan.AllianceOffers.Length, tribeId, 0,
                        $"tribe {tribeId} point total", expectedRevision);

                AdvanceRevisionAfterSuccessfulWrite(ref expectedRevision);
                firstPointTotalTribeId = tribeId + 1;
            }

            for (var tribeId = 0; tribeId < TribeCount; tribeId++)
            {
                if (plan.PointDeltas[tribeId] == 0)
                    continue;

                firstPointDeltaTribeId = tribeId;
                var updated = await repository.TryAddTribePointsAsync((byte)tribeId, plan.PointDeltas[tribeId],
                        expectedRevision, ct)
                    .ConfigureAwait(false);
                if (!updated)
                    return Conflict(plan, false, plan.TribeStates.Length, plan.AllianceOffers.Length, TribeCount, tribeId,
                        $"tribe {tribeId} point delta", expectedRevision);

                AdvanceRevisionAfterSuccessfulWrite(ref expectedRevision);
                firstPointDeltaTribeId = tribeId + 1;
            }
        }
        catch (OperationCanceledException)
        {
            RequeueUnpersistedPlan(plan, !worldCommitted, firstTribeStateIndex, firstAllianceOfferIndex,
                firstPointTotalTribeId, firstPointDeltaTribeId);
            throw;
        }
        catch (Exception ex)
        {
            RequeueUnpersistedPlan(plan, !worldCommitted, firstTribeStateIndex, firstAllianceOfferIndex,
                firstPointTotalTribeId, firstPointDeltaTribeId);
            logger.LogError(ex, "WorldState flush failed -- dirty mutations remain queued for retry");
            return FlushPlanResult.Failed;
        }

        return FlushPlanResult.Succeeded;
    }

    private FlushPlanResult Conflict(FlushPlan plan, bool includeWorld, int firstTribeStateIndex,
        int firstAllianceOfferIndex, int firstPointTotalTribeId, int firstPointDeltaTribeId, string mutation,
        long expectedRevision)
    {
        RequeueUnpersistedPlan(plan, includeWorld, firstTribeStateIndex, firstAllianceOfferIndex,
            firstPointTotalTribeId, firstPointDeltaTribeId);
        logger.LogWarning(
            "WorldState flush {Mutation} conflicted at revision {ExpectedRevision}; rebasing retained mutations",
            mutation, expectedRevision);
        return FlushPlanResult.Conflict;
    }

    private async ValueTask<bool> ReconcileCoreAsync(CancellationToken ct)
    {
        if (!_initialized)
            return true;

        long revisionBeforeRead;
        lock (_lock)
        {
            revisionBeforeRead = _revision;
        }

        WorldStateRowDto? row;
        ImmutableArray<WorldStateTribeDto> tribes;
        ImmutableArray<WorldStateAllianceOfferDto> allianceOffers;

        try
        {
            (row, tribes, allianceOffers) = await repository.GetAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "WorldState reconcile read failed -- dirty mutations remain queued for retry");
            return false;
        }

        if (row is null)
        {
            logger.LogError(
                "WorldState reconcile: game.WorldState has no singleton row -- dirty mutations remain queued for retry");
            return false;
        }

        lock (_lock)
        {
            if (_revision != revisionBeforeRead)
                return true;

            _revision = row.Revision;
            _world = new WorldRvrState(row.Zone038WinTribe, row.Zone038WinTribeTime, row.TribeSymbolBattle,
                row.MonsterSymbol, row.MonsterSymbolEndTime, row.HighTribe, row.UpdateTribePoint);

            foreach (var tribe in tribes)
            {
                if (tribe.TribeId >= TribeCount)
                    continue;

                var rebasedPoints = (_pendingTribePointTotals[tribe.TribeId] ?? tribe.Points) +
                                    _pendingTribePointDeltas[tribe.TribeId];
                _tribes[tribe.TribeId] = new TribeRvrState(tribe.TribeId, tribe.SymbolDateUtc, tribe.HasSymbol,
                    rebasedPoints, tribe.IsClosed);
                _tribeSymbolOwner[tribe.TribeId] = tribe.SymbolOwnerTribeId;
            }

            _allianceOffers.Clear();
            foreach (var offer in allianceOffers)
                _allianceOffers[(offer.FromTribeId, offer.ToTribeId)] =
                    new AllianceOfferState(offer.FromTribeId, offer.ToTribeId, offer.IsAccepted);

            if (_pendingWorld is { } pendingWorld)
                _world = pendingWorld.State;

            foreach (var pendingTribe in _pendingTribeStates.Values)
            {
                var tribeId = pendingTribe.State.TribeId;
                _tribes[tribeId] = _tribes[tribeId] with
                {
                    TribeId = tribeId,
                    SymbolDate = pendingTribe.State.SymbolDate,
                    HasSymbol = pendingTribe.State.HasSymbol,
                    IsClosed = pendingTribe.State.IsClosed
                };
                _tribeSymbolOwner[tribeId] = pendingTribe.SymbolOwner;
            }

            foreach (var pendingOffer in _pendingAllianceOffers.Values)
            {
                var offer = pendingOffer.State;
                _allianceOffers[(offer.FromTribeId, offer.ToTribeId)] = offer;
            }

            RefreshDirtyLocked();
        }

        return true;
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

    private async ValueTask RetainTribePointTotalsAfterFailedWriteAsync(IReadOnlyList<int> requestedTotals,
        CancellationToken ct)
    {
        if (!await ReconcileCoreAsync(ct).ConfigureAwait(false))
        {
            lock (_lock)
            {
                for (var tribeId = 0; tribeId < TribeCount; tribeId++)
                    _pendingTribePointTotals[tribeId] = requestedTotals[tribeId];

                RefreshDirtyLocked();
            }

            return;
        }

        lock (_lock)
        {
            for (var tribeId = 0; tribeId < TribeCount; tribeId++)
            {
                var requestedTotalWithNewDeltas = requestedTotals[tribeId] + _pendingTribePointDeltas[tribeId];
                var delta = requestedTotalWithNewDeltas - _tribes[tribeId].Points;
                _tribes[tribeId] = _tribes[tribeId] with
                {
                    TribeId = (byte)tribeId,
                    Points = requestedTotalWithNewDeltas
                };
                _pendingTribePointDeltas[tribeId] += delta;
            }

            RefreshDirtyLocked();
        }
    }

    private void QueueWorldMutation()
    {
        _pendingWorld = new PendingWorldMutation(NextMutationVersion(), _world);
        RefreshDirtyLocked();
    }

    private void QueueTribeStateMutation(byte tribeId)
    {
        _pendingTribeStates[tribeId] =
            new PendingTribeStateMutation(NextMutationVersion(), _tribes[tribeId], _tribeSymbolOwner[tribeId]);
        RefreshDirtyLocked();
    }

    private void QueueAllianceOfferMutation(byte fromTribeId, byte toTribeId)
    {
        var key = (fromTribeId, toTribeId);
        _pendingAllianceOffers[key] = new PendingAllianceOfferMutation(NextMutationVersion(), _allianceOffers[key]);
        RefreshDirtyLocked();
    }

    private void RequeueUnpersistedPlan(FlushPlan plan, bool includeWorld, int firstTribeStateIndex,
        int firstAllianceOfferIndex, int firstPointTotalTribeId, int firstPointDeltaTribeId)
    {
        lock (_lock)
        {
            if (includeWorld && plan.World is { } world && _pendingWorld is null)
                _pendingWorld = world;

            for (var tribeIndex = firstTribeStateIndex; tribeIndex < plan.TribeStates.Length; tribeIndex++)
            {
                var tribe = plan.TribeStates[tribeIndex];
                _pendingTribeStates.TryAdd(tribe.State.TribeId, tribe);
            }

            for (var offerIndex = firstAllianceOfferIndex; offerIndex < plan.AllianceOffers.Length; offerIndex++)
            {
                var offer = plan.AllianceOffers[offerIndex];
                _pendingAllianceOffers.TryAdd((offer.State.FromTribeId, offer.State.ToTribeId), offer);
            }

            for (var tribeId = firstPointTotalTribeId; tribeId < TribeCount; tribeId++)
            {
                if (plan.PointTotals[tribeId] is { } total && _pendingTribePointTotals[tribeId] is null)
                    _pendingTribePointTotals[tribeId] = total;
            }

            for (var tribeId = firstPointDeltaTribeId; tribeId < TribeCount; tribeId++)
                _pendingTribePointDeltas[tribeId] += plan.PointDeltas[tribeId];

            RefreshDirtyLocked();
        }
    }

    private long NextMutationVersion() => ++_nextMutationVersion;

    private void RefreshDirtyLocked()
    {
        _dirty = _pendingWorld.HasValue || _pendingTribeStates.Count != 0 || _pendingAllianceOffers.Count != 0 ||
                 Array.Exists(_pendingTribePointTotals, static total => total.HasValue) ||
                 Array.Exists(_pendingTribePointDeltas, static delta => delta != 0);
    }

    private static TimeSpan ConflictReplayBackoff(int conflictAttempt) =>
        TimeSpan.FromMilliseconds(25 * (1 << conflictAttempt));

    private enum FlushPlanResult
    {
        Succeeded,
        Conflict,
        Failed
    }

    private readonly record struct PendingWorldMutation(long Version, WorldRvrState State);

    private readonly record struct PendingTribeStateMutation(long Version, TribeRvrState State, byte SymbolOwner);

    private readonly record struct PendingAllianceOfferMutation(long Version, AllianceOfferState State);

    private sealed record FlushPlan(long ExpectedRevision, PendingWorldMutation? World,
        PendingTribeStateMutation[] TribeStates, PendingAllianceOfferMutation[] AllianceOffers,
        int?[] PointTotals, int[] PointDeltas);

    private void AdvanceRevisionAfterSuccessfulWrite(ref long expectedRevision)
    {
        lock (_lock)
        {
            UpdateRevisionAfterSuccessfulWrite(expectedRevision);
        }

        expectedRevision++;
    }

    private void UpdateRevisionAfterSuccessfulWrite(long expectedRevision)
    {
        _revision = Math.Max(_revision, expectedRevision + 1);
    }

    private static int NowAsLegacyHhMm()
    {
        var now = DateTime.UtcNow;
        return now.Hour * 100 + now.Minute;
    }
}
