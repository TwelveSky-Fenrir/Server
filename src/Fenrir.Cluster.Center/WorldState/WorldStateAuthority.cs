using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Center.WorldState;

public sealed class WorldStateAuthority(IWorldStateRepository repository, ILogger<WorldStateAuthority> logger)
    : IWorldStateAuthority
{
    private const int TribeCount = FavoredTribeRankLadder.TribeCount;

    private const int ZoneStateSlots = 350;
    private const int VoteSlotMax = 32;

    private readonly Dictionary<(byte From, byte To), CenterAllianceOffer> _allianceOffers = new();
    private readonly float[] _generalExperienceUpRatio = new float[TribeCount];
    private readonly float[] _itemDropUpForMyoungRatio = new float[TribeCount];
    private readonly float[] _itemDropUpRatio = new float[TribeCount];
    private readonly int[] _killOtherTribeAddValue = new int[TribeCount];

    private readonly Lock _lock = new();
    private readonly int[] _tribeDtmValue = new int[TribeCount];
    private readonly byte[,] _tribeGuard = new byte[TribeCount, TribeCount];
    private readonly int[] _tribeMasterCallAbility = new int[TribeCount];
    private readonly CenterTribeRow[] _tribes = new CenterTribeRow[TribeCount];
    private readonly Dictionary<(byte Tribe, byte Slot), string> _tribeSubMaster = new();
    private readonly byte[] _tribeSymbol = new byte[TribeCount];
    private readonly byte[] _tribeVoteState = new byte[TribeCount];
    private readonly Dictionary<(int I1, int I2), byte> _zone175 = new();

    private readonly Dictionary<WorldZoneStateKind, byte[]> _zoneStates = new();
    private readonly Dictionary<WorldZoneStateKind, int[]> _zoneStateTimes = new();

    private bool _dirty;
    private long _flushSequence;
    private bool _initialized;
    private CenterWorldRow _world = new(null, null, false, null, null, null, 0);

    public byte? HighTribe
    {
        get
        {
            lock (_lock)
            {
                return _world.HighTribe;
            }
        }
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_initialized)
            throw new InvalidOperationException("WorldStateAuthority.InitializeAsync must only run once, at boot.");

        await repository.EnsureInitializedAsync(ct).ConfigureAwait(false);
        var (row, tribes, allianceOffers) = await repository.GetAsync(ct).ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException(
                "game.WorldState has no singleton row right after EnsureInitialized -- Center boot invariant violated.");

        lock (_lock)
        {
            _world = new CenterWorldRow(row.Zone038WinTribe, row.Zone038WinTribeTime, row.TribeSymbolBattle,
                row.MonsterSymbol, row.MonsterSymbolEndTime, row.HighTribe, row.UpdateTribePoint);

            for (byte i = 0; i < TribeCount; i++)
                _tribes[i] = new CenterTribeRow(i, null, true, 0, false);

            foreach (var tribe in tribes)
                if (tribe.TribeId < TribeCount)
                    _tribes[tribe.TribeId] =
                        new CenterTribeRow(tribe.TribeId, tribe.SymbolDateUtc, tribe.HasSymbol, tribe.Points,
                            tribe.IsClosed);

            foreach (var offer in allianceOffers)
                _allianceOffers[(offer.FromTribeId, offer.ToTribeId)] =
                    new CenterAllianceOffer(offer.FromTribeId, offer.ToTribeId, offer.IsAccepted);

            foreach (var zone in new[]
                     {
                         WorldZoneStateKind.Zone049, WorldZoneStateKind.Zone051, WorldZoneStateKind.Zone053,
                         WorldZoneStateKind.Zone267, WorldZoneStateKind.Zone241
                     })
            {
                _zoneStates[zone] = new byte[ZoneStateSlots];
                _zoneStateTimes[zone] = new int[ZoneStateSlots];
            }

            _zoneStates[WorldZoneStateKind.Zone194] = new byte[1];
            _zoneStateTimes[WorldZoneStateKind.Zone194] = new int[1];
            _zoneStates[WorldZoneStateKind.ZoneFfa335] = new byte[1];
            _zoneStateTimes[WorldZoneStateKind.ZoneFfa335] = new int[1];

            _initialized = true;
            _dirty = false;
        }

        logger.LogInformation(
            "Center WorldState loaded: Zone038WinTribe={Zone038WinTribe} TribeSymbolBattle={TribeSymbolBattle} " +
            "HighTribe={HighTribe} AllianceOffers={AllianceOfferCount}",
            row.Zone038WinTribe, row.TribeSymbolBattle, row.HighTribe, allianceOffers.Length);
    }


    public void SetZone049State(int index, int state)
    {
        if (!SetZoneTypeState(WorldZoneStateKind.Zone049, index, (byte)state, false))
            logger.LogWarning("SetZone049State dropped: index {Index} out of bounds [0,{Bound})", index,
                ZoneStateSlots);
    }

    public bool SetZoneTypeState(WorldZoneStateKind zone, int index, byte state, bool stampTime)
    {
        lock (_lock)
        {
            if (!_zoneStates.TryGetValue(zone, out var states) || index < 0 || index >= states.Length)
                return false;

            states[index] = state;
            if (stampTime)
                _zoneStateTimes[zone][index] = NowAsLegacyHhMm();
            return true;
        }
    }

    public bool SetZone175TypeState(int i1, int i2, byte state)
    {
        if (i1 < 0 || i1 >= ZoneStateSlots || i2 < 0 || i2 >= ZoneStateSlots)
            return false;

        lock (_lock)
        {
            _zone175[(i1, i2)] = state;
            return true;
        }
    }

    public bool SetTribeGuardState(int fromTribe, int toTribe, byte state)
    {
        if (fromTribe < 0 || fromTribe >= TribeCount || toTribe < 0 || toTribe >= TribeCount)
            return false;

        lock (_lock)
        {
            _tribeGuard[fromTribe, toTribe] = state;
            return true;
        }
    }


    public bool SetSymbolWinTribe(byte tribe)
    {
        if (tribe >= TribeCount)
            return false;

        lock (_lock)
        {
            _world = _world with { Zone038WinTribe = tribe, Zone038WinTribeTime = NowAsLegacyHhMm() };
            _dirty = true;
        }

        return true;
    }

    public void StartSymbolBattle()
    {
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            _world = _world with { TribeSymbolBattle = true };
            for (byte i = 0; i < TribeCount; i++)
            {
                _tribes[i] = _tribes[i] with { HasSymbol = true, SymbolDate = now };
                _tribeSymbol[i] = i;
            }

            _dirty = true;
        }
    }

    public bool SetTribeSymbol(int symbolIndex, byte winnerTribe)
    {
        if (symbolIndex < 0 || symbolIndex >= TribeCount || winnerTribe >= TribeCount)
            return false;

        lock (_lock)
        {
            _tribeSymbol[symbolIndex] = winnerTribe;
            _tribes[symbolIndex] = _tribes[symbolIndex] with
            {
                HasSymbol = winnerTribe == symbolIndex, SymbolDate = DateTime.UtcNow
            };
            _dirty = true;
        }

        return true;
    }

    public bool SetMonsterSymbol(byte winnerTribe)
    {
        if (winnerTribe >= TribeCount)
            return false;

        lock (_lock)
        {
            _world = _world with { MonsterSymbol = winnerTribe, MonsterSymbolEndTime = NowAsLegacyHhMm() };
            _dirty = true;
        }

        return true;
    }

    public void EndSymbolBattle()
    {
        lock (_lock)
        {
            _world = _world with { TribeSymbolBattle = false };
            Array.Clear(_tribeMasterCallAbility);
            _dirty = true;
        }
    }


    public bool SetAllianceState(byte tribe1, byte tribe2)
    {
        if (tribe1 >= TribeCount || tribe2 >= TribeCount || tribe1 == tribe2)
            return false;

        lock (_lock)
        {
            _allianceOffers[(tribe1, tribe2)] = new CenterAllianceOffer(tribe1, tribe2, true);
            _dirty = true;
        }

        return true;
    }

    public bool SetPossibleAlliance(byte tribe1, int date1, byte tribe2, int date2)
    {
        if (tribe1 >= TribeCount || tribe2 >= TribeCount || tribe1 == tribe2)
            return false;

        lock (_lock)
        {
            _allianceOffers[(tribe1, tribe2)] = new CenterAllianceOffer(tribe1, tribe2, false);
            _allianceOffers[(tribe2, tribe1)] = new CenterAllianceOffer(tribe2, tribe1, false);
            _dirty = true;
        }

        return true;
    }


    public bool SetTribeVoteState(int tribe, byte state)
    {
        if (tribe < 0 || tribe >= TribeCount)
            return false;

        lock (_lock)
        {
            _tribeVoteState[tribe] = state;
        }

        return true;
    }

    public ValueTask RegisterTribeVoteCandidateAsync(byte tribe, byte slotIndex, int candidateCharacterId,
        short candidateLevel, int killOtherTribeCount, CancellationToken ct)
    {
        if (tribe >= TribeCount)
            return ValueTask.CompletedTask;

        return repository.RegisterTribeVoteCandidateAsync(tribe, slotIndex, candidateCharacterId, candidateLevel,
            killOtherTribeCount, ct);
    }

    public ValueTask ClearTribeVoteCandidateAsync(byte tribe, byte slotIndex, CancellationToken ct)
    {
        if (tribe >= TribeCount)
            return ValueTask.CompletedTask;

        return repository.RegisterTribeVoteCandidateAsync(tribe, slotIndex, 0, 0, 0, ct);
    }

    public ValueTask AddTribeVotePointsAsync(byte tribe, byte slotIndex, int points, CancellationToken ct)
    {
        if (tribe >= TribeCount)
            return ValueTask.CompletedTask;

        return repository.AddTribeVotePointsAsync(tribe, slotIndex, points, ct);
    }

    public ValueTask PurgeTribeVoteCandidatesAsync(byte tribe, CancellationToken ct)
    {
        if (tribe >= TribeCount)
            return ValueTask.CompletedTask;

        return repository.ClearTribeVotesAsync(tribe, ct);
    }

    public bool SetTribeSubMaster(byte tribe, byte slotIndex, string name)
    {
        if (tribe >= TribeCount || slotIndex >= VoteSlotMax)
            return false;

        lock (_lock)
        {
            _tribeSubMaster[(tribe, slotIndex)] = name;
        }

        return true;
    }

    public bool ClearTribeSubMaster(byte tribe, byte slotIndex)
    {
        if (tribe >= TribeCount || slotIndex >= VoteSlotMax)
            return false;

        lock (_lock)
        {
            _tribeSubMaster.Remove((tribe, slotIndex));
        }

        return true;
    }


    public bool SetTribeRatioBonus(TribeRatioBonusKind kind, byte tribe, int rawValue)
    {
        if (tribe >= TribeCount)
            return false;

        var scaled = rawValue * 0.1f;
        lock (_lock)
        {
            switch (kind)
            {
                case TribeRatioBonusKind.GeneralExperienceUp:
                    _generalExperienceUpRatio[tribe] = scaled;
                    break;
                case TribeRatioBonusKind.ItemDropUp:
                    _itemDropUpRatio[tribe] = scaled;
                    break;
                case TribeRatioBonusKind.ItemDropUpForMyoung:
                    _itemDropUpForMyoungRatio[tribe] = scaled;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    public bool SetTribeKillOtherTribeAddValueExclusive(byte tribe, int value)
    {
        if (tribe >= TribeCount)
            return false;

        lock (_lock)
        {
            Array.Clear(_killOtherTribeAddValue);
            _killOtherTribeAddValue[tribe] = value;
        }

        return true;
    }

    public bool SetTribeMasterCallAbility(byte tribe, int skillSort)
    {
        if (tribe >= TribeCount)
            return false;

        lock (_lock)
        {
            _tribeMasterCallAbility[tribe] = skillSort;
        }

        return true;
    }

    public bool SetTribeDtmValue(byte tribe, int dtm)
    {
        if (tribe >= TribeCount)
            return false;

        lock (_lock)
        {
            _tribeDtmValue[tribe] = dtm;
        }

        return true;
    }


    public void SetUpdateTribePointFlag(short value)
    {
        lock (_lock)
        {
            _world = _world with { UpdateTribePoint = value };
            _dirty = true;
        }
    }

    public bool TryConsumeUpdateTribePointFlag(short pendingValue, short consumedValue)
    {
        lock (_lock)
        {
            if (_world.UpdateTribePoint != pendingValue)
                return false;

            _world = _world with { UpdateTribePoint = consumedValue };
            _dirty = true;
            return true;
        }
    }

    public int[] SnapshotTribePoints()
    {
        lock (_lock)
        {
            var totals = new int[TribeCount];
            for (var i = 0; i < TribeCount; i++)
                totals[i] = _tribes[i].Points;
            return totals;
        }
    }

    public void OverwriteTribePoints(ReadOnlySpan<int> totals)
    {
        if (totals.Length != TribeCount)
            throw new ArgumentException($"Expected exactly {TribeCount} totals.", nameof(totals));

        lock (_lock)
        {
            for (byte i = 0; i < TribeCount; i++)
                _tribes[i] = _tribes[i] with { Points = totals[i] };
            _dirty = true;
        }
    }

    public void SetHighTribe(byte? tribeId)
    {
        if (tribeId is { } id && id >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");

        lock (_lock)
        {
            _world = _world with { HighTribe = tribeId };
            _dirty = true;
        }
    }


    public async ValueTask FlushIfDirtyAsync(CancellationToken ct)
    {
        CenterWorldRow world;
        CenterTribeRow[] tribes;
        CenterAllianceOffer[] offers;

        lock (_lock)
        {
            if (!_dirty)
                return;

            world = _world;
            tribes = (CenterTribeRow[])_tribes.Clone();
            offers = [.. _allianceOffers.Values];
        }

        try
        {
            await repository.UpdateAsync(world.Zone038WinTribe, world.Zone038WinTribeTime, world.TribeSymbolBattle,
                    world.MonsterSymbol, world.MonsterSymbolEndTime, world.HighTribe, world.UpdateTribePoint, ct)
                .ConfigureAwait(false);

            foreach (var tribe in tribes)
                await repository.UpdateTribeAsync(tribe.TribeId, tribe.SymbolDate, tribe.HasSymbol, tribe.Points,
                    tribe.IsClosed, ct).ConfigureAwait(false);

            foreach (var offer in offers)
                await repository.SetAllianceOfferAsync(offer.FromTribeId, offer.ToTribeId, offer.IsAccepted, ct)
                    .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Center WorldState flush failed -- staying dirty, retrying next interval");
            return;
        }

        long sequence;
        lock (_lock)
        {
            _dirty = false;
            sequence = ++_flushSequence;
        }

        logger.LogDebug("Center WorldState flush #{Sequence} committed", sequence);
    }

    private static int NowAsLegacyHhMm()
    {
        var now = DateTime.UtcNow;
        return now.Hour * 100 + now.Minute;
    }
}
