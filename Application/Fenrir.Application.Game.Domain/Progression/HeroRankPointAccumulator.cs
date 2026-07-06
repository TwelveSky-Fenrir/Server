using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Progression;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Process-wide write-behind accumulator for hero-rank points earned through the PvP-kill reward pipeline
///     (<see cref="Fenrir.Application.Game.Domain.World.Zone" />'s <c>ApplyPvpKillHeroPoints</c>). Same "hot
///     mutable counter batched instead of a DB write per grant" posture as <see cref="TowerWarState" />/
///     <c>WorldStateService</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why accumulate-then-flush instead of writing straight through:</b> <c>game.usp_HeroRanking_Upsert</c>
///         replaces the whole Points column (a claim-time overwrite, correct for
///         <c>HeroRewardClaimService</c>'s use), so it can never safely be called from a per-kill grant path
///         -- two grants racing each other through a read-modify-write in application code would silently
///         drop one. <c>game.usp_HeroRanking_AddPoints</c> (new) does the accumulate atomically in SQL
///         instead, so this class only needs to batch how many grants happened between flushes, not
///         coordinate a read.
///     </para>
///     <para>
///         Points earned between flushes are visible immediately to the earning character via
///         <see cref="PlayerRuntimeState.HeroRankPoints" /> (updated synchronously by the same tick that
///         calls <see cref="AddPending" />) -- this class only defers the DB write, never the in-memory
///         total the player's own session already reflects.
///     </para>
/// </remarks>
public sealed class HeroRankPointAccumulator(ILogger<HeroRankPointAccumulator>? logger = null)
{
    /// <summary>Hero points are only ever earned into the live (Current) ranking period.</summary>
    public const byte CurrentPeriodKind = 0;

    private readonly Lock _lock = new();
    private readonly Dictionary<int, (byte? TribeId, int? Level)> _pendingContext = new();
    private readonly Dictionary<int, int> _pendingDelta = new();

    /// <summary>
    ///     Records <paramref name="delta" /> more points earned by <paramref name="characterId" /> since the
    ///     last flush -- accumulates in memory if called again before the next flush. <paramref name="tribeId" />/
    ///     <paramref name="level" /> are the character's CURRENT values at grant time (refreshed on every
    ///     call), so the eventually-flushed row reflects whichever grant happened last, not the first.
    /// </summary>
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

    /// <summary>
    ///     Flushes every pending delta as one atomic <c>usp_HeroRanking_AddPoints</c> call per character.
    ///     Never throws -- a failed character's delta is re-merged for the next interval's attempt (added back
    ///     on top of anything accumulated in the meantime), matching <see cref="TowerWarState.FlushDirtyAsync" />'s
    ///     own "log and retry next interval" contract.
    /// </summary>
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
