using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Guilds;

/// <summary>
///     Four-guild live-scoring data access (C17 Part B): the enemy-tribe-kill point accrual and the
///     top-N-positive leaderboard read. Kept separate from <see cref="IGuildRepository" /> because both
///     operations have deliberately different semantics from the general guild-point surface there --
///     the accrual is silent on an unknown guild (vs. <c>AdjustPointsAsync</c>'s 50234 THROW), and the
///     leaderboard filters to strictly-positive points (vs. <c>GetTopByPointsAsync</c>'s unconditional top N).
/// </summary>
public interface IFourGuildScoringRepository
{
    /// <summary>
    ///     Credits guild <paramref name="guildId" /> +<paramref name="delta" /> four-guild points
    ///     (game.usp_Guild_AddFourGuildPoints). Silent by contract: an unknown/disbanded guild matches no row
    ///     and the point is lost with no error (never a THROW). The sole live caller adds exactly +1 per
    ///     enemy-tribe kill scored in the zone-049 event mode.
    /// </summary>
    public ValueTask AddPointsAsync(int guildId, int delta, CancellationToken ct);

    /// <summary>
    ///     The top <paramref name="count" /> guilds with a strictly-positive point total, highest first
    ///     (game.usp_Guild_GetTopFourGuild) -- the four-guild leaderboard standings copied into the shared
    ///     display/broadcast state.
    /// </summary>
    public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetLeaderboardAsync(int count, CancellationToken ct);
}
