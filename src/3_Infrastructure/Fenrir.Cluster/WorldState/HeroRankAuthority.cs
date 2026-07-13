using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.WorldState;

/// <summary>
///     The CenterServer's single authoritative writer of hero-rank point accrual. Holds each character's
///     current-period running total in memory (single writer), accumulates on every reported gain, and flushes
///     the changed entries on the ~6s cadence. CORRECTS legacy Bug 1: the reported level is tracked
///     monotonically instead of being frozen at the first gain of the cycle.
/// </summary>
/// <remarks>
///     Reimplemented from <c>AddOrUpdateHeroRank</c> (Server/ts25center/S08_MyDB.cpp:213-216). The legacy upsert
///     accumulates points (<c>hPoint += delta</c>) but omits <c>hLevel</c> from the duplicate-key update, so the
///     stored level freezes forever. Here the authoritative total lives in memory and the flush wholesale-writes
///     it, so a re-flush is idempotent and the level always reflects the highest level seen.
/// </remarks>
public sealed class HeroRankAuthority(ICenterHeroRankStore store, ILogger<HeroRankAuthority> logger)
    : IHeroRankAuthority
{
    private const int TribeCount = FavoredTribeRankLadder.TribeCount;

    private readonly Lock _lock = new();
    private readonly Dictionary<int, Accrual> _accruals = new();

    private bool _initialized;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_initialized)
            throw new InvalidOperationException("HeroRankAuthority.InitializeAsync must only run once, at boot.");

        var rows = await store.LoadCurrentPeriodAsync(ct).ConfigureAwait(false);

        lock (_lock)
        {
            foreach (var row in rows)
                _accruals[row.CharacterId] = new Accrual
                {
                    Tribe = row.TribeId,
                    Points = row.Points,
                    Level = row.Level,
                    Dirty = false
                };

            _initialized = true;
        }

        logger.LogInformation("Center HeroRank loaded: {Count} current-period accrual rows", rows.Count);
    }

    public void AddOrUpdate(int hPoint, byte hTribe, int hLevel, int uCharIdx)
    {
        if (hTribe >= TribeCount)
        {
            logger.LogWarning("HeroRank AddOrUpdate dropped: character {CharacterId} reported tribe {Tribe} out of range",
                uCharIdx, hTribe);
            return;
        }

        lock (_lock)
        {
            if (_accruals.TryGetValue(uCharIdx, out var accrual))
            {
                accrual.Points += hPoint;
                accrual.Tribe = hTribe;
                accrual.Level = Math.Max(accrual.Level, hLevel); // Bug 1 fix: monotonic level, never regresses.
                accrual.Dirty = true;
            }
            else
            {
                _accruals[uCharIdx] = new Accrual
                {
                    Tribe = hTribe,
                    Points = hPoint,
                    Level = hLevel,
                    Dirty = true
                };
            }
        }
    }

    public async ValueTask FlushDirtyAsync(CancellationToken ct)
    {
        List<(int CharacterId, Accrual Snapshot)> pending = [];

        lock (_lock)
        {
            foreach (var (characterId, accrual) in _accruals)
            {
                if (!accrual.Dirty)
                    continue;

                pending.Add((characterId, new Accrual
                {
                    Tribe = accrual.Tribe, Points = accrual.Points, Level = accrual.Level, Dirty = false
                }));
            }
        }

        foreach (var (characterId, snapshot) in pending)
            try
            {
                await store.UpsertCurrentPeriodAsync(characterId, snapshot.Points, snapshot.Tribe, snapshot.Level, ct)
                    .ConfigureAwait(false);

                lock (_lock)
                {
                    // Only clear dirty if no newer gain landed while we were flushing (points still match).
                    if (_accruals.TryGetValue(characterId, out var current) && current.Points == snapshot.Points &&
                        current.Level == snapshot.Level)
                        current.Dirty = false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Center HeroRank flush failed for character {CharacterId} -- left dirty, retried next interval",
                    characterId);
            }
    }

    private sealed class Accrual
    {
        public byte Tribe { get; set; }
        public int Points { get; set; }
        public int Level { get; set; }
        public bool Dirty { get; set; }
    }
}
