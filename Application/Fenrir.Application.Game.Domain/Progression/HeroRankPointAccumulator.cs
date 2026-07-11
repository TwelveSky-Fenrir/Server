using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Progression;

public sealed class HeroRankPointAccumulator(ILogger<HeroRankPointAccumulator>? logger = null)
{

        public const byte CurrentPeriodKind = 0;

    private readonly Lock _lock = new();
    private readonly Dictionary<int, (byte? TribeId, int? Level)> _pendingContext = new();
    private readonly Dictionary<int, int> _pendingDelta = new();

        public void AddPending(int characterId, int delta, byte? tribeId, int? level)
    {
        if (delta == 0)
            return;

        lock (_lock)
        {
            _pendingDelta[characterId] = _pendingDelta.GetValueOrDefault(characterId) + delta;
            _pendingContext[characterId] = (tribeId, level);
        }
    }

        public async Task FlushDirtyAsync(IHeroRankingRepository heroRankings, CancellationToken ct)
    {
        Dictionary<int, int> deltaSnapshot;
        Dictionary<int, (byte? TribeId, int? Level)> contextSnapshot;

        lock (_lock)
        {
            if (_pendingDelta.Count == 0)
                return;

            deltaSnapshot = new Dictionary<int, int>(_pendingDelta);
            contextSnapshot = new Dictionary<int, (byte?, int?)>(_pendingContext);
            _pendingDelta.Clear();
            _pendingContext.Clear();
        }

        foreach (var (characterId, delta) in deltaSnapshot)
        {
            var (tribeId, level) = contextSnapshot.TryGetValue(characterId, out var ctx) ? ctx : (null, null);

            try
            {
                await heroRankings.AddPointsAsync(characterId, CurrentPeriodKind, delta, tribeId, level, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _pendingDelta[characterId] = _pendingDelta.GetValueOrDefault(characterId) + delta;
                    _pendingContext[characterId] = (tribeId, level);
                }

                logger?.LogError(ex,
                    "HeroRanking point flush failed for character {CharacterId} -- delta {Delta} re-queued",
                    characterId, delta);
            }
        }
    }
}
