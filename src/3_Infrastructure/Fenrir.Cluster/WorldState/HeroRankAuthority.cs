using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.WorldState;

public sealed class HeroRankAuthority(ICenterHeroRankStore store, ILogger<HeroRankAuthority> logger)
    : IHeroRankAuthority
{
    private const int TribeCount = FavoredTribeRankLadder.TribeCount;
    private readonly Dictionary<int, Accrual> _accruals = new();

    private readonly Lock _lock = new();

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
            logger.LogWarning(
                "HeroRank AddOrUpdate dropped: character {CharacterId} reported tribe {Tribe} out of range",
                uCharIdx, hTribe);
            return;
        }

        lock (_lock)
        {
            if (_accruals.TryGetValue(uCharIdx, out var accrual))
            {
                accrual.Points += hPoint;
                accrual.Tribe = hTribe;
                accrual.Level = Math.Max(accrual.Level, hLevel);
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
