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

    private readonly int[] _pendingTribePointDeltas = new int[TribeCount];

    private readonly byte[] _tribeFormationAbility = new byte[TribeCount];

    private readonly TribeRvrState[] _tribes = new TribeRvrState[TribeCount];

    private readonly byte[] _tribeSymbolOwner = new byte[TribeCount];

    private bool _dirty;
    private bool _initialized;

    private int _scalarVersion;

    private WorldRvrState _world;

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
                    _tribes[tribe.TribeId] =
                        new TribeRvrState(tribe.TribeId, tribe.SymbolDateUtc, tribe.HasSymbol, tribe.Points,
                            tribe.IsClosed);

            for (byte i = 0; i < TribeCount; i++)
                _tribeSymbolOwner[i] = i;

            foreach (var offer in allianceOffers)
                _allianceOffers[(offer.FromTribeId, offer.ToTribeId)] =
                    new AllianceOfferState(offer.FromTribeId, offer.ToTribeId, offer.IsAccepted);

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
            _dirty = true;
            _scalarVersion++;
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
            }

            _dirty = true;
            _scalarVersion++;
        }

        logger.LogInformation("WorldState: tribe symbol battle window opened at {SymbolBattleStarted:O}", now);
    }

    public void EndTribeSymbolBattle()
    {
        lock (_lock)
        {
            _world = _world with { TribeSymbolBattle = false };
            Array.Clear(_tribeFormationAbility);
            _dirty = true;
            _scalarVersion++;
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
            _dirty = true;
            _scalarVersion++;
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
            _dirty = true;
            _scalarVersion++;
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
            _dirty = true;
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

    public void SetUpdateTribePointFlag(short value)
    {
        lock (_lock)
        {
            _world = _world with { UpdateTribePoint = value };
            _dirty = true;
            _scalarVersion++;
        }
    }

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

        return allSucceeded;
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
            _dirty = true;
            _scalarVersion++;
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
                _dirty = true;
                _scalarVersion++;
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
        ImmutableArray<WorldStateTribeDto> tribes;
        ImmutableArray<WorldStateAllianceOfferDto> allianceOffers;

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
                        SymbolDate = tribe.SymbolDateUtc,
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

    private static int NowAsLegacyHhMm()
    {
        var now = DateTime.UtcNow;
        return now.Hour * 100 + now.Minute;
    }
}
