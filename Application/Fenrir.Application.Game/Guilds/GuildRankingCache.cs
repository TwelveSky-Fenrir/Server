using System.Collections.Immutable;
using Fenrir.Data.Guilds;

namespace Fenrir.Application.Game.Guilds;

/// <summary>
///     Process-wide, thread-safe snapshot of the RvR guild ranking board (game.Guilds.Points via
///     game.usp_Guild_GetTopByPoints), mirrored onto WorldInfo.GuildName3/GuildScore by
///     <see cref="GuildRankingProjection" /> at world-entry/zone-move. Same "read synchronously, refreshed by a
///     periodic background pass" shape as <see cref="World.WorldState.WorldStateService" />'s own cache, chosen
///     over a live per-request query so op12/op20 never gain an extra SQL round trip on their own hot path.
/// </summary>
public sealed class GuildRankingCache
{
    /// <summary>WorldInfo.GuildName3/GuildScore are both fixed 3-element arrays -- the wire shape, not a tuning knob.</summary>
    public const int TopCount = 3;

    private readonly Lock _lock = new();
    private ImmutableArray<GuildRankingRowDto> _top = [];

    public ImmutableArray<GuildRankingRowDto> Top
    {
        get
        {
            lock (_lock)
            {
                return _top;
            }
        }
    }

    /// <summary>Re-queries the top <see cref="TopCount" /> guilds and atomically swaps them in.</summary>
    public async Task RefreshAsync(IGuildRepository guilds, CancellationToken ct)
    {
        var top = await guilds.GetTopByPointsAsync(TopCount, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _top = [..top];
        }
    }
}
