using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Guilds;

public readonly record struct FourGuildStanding(int GuildId, string Name, int Points);

public sealed class FourGuildScoringService(
    IFourGuildScoringRepository scoring,
    ILogger<FourGuildScoringService> logger)
{

        public const int KillPointDelta = 1;

        public const int LeaderboardSize = 3;

    private volatile IReadOnlyList<FourGuildStanding> _standings = [];

        public IReadOnlyList<FourGuildStanding> CurrentStandings => _standings;

        public async Task AccrueKillPointAsync(int guildId, CancellationToken ct)
    {
        try
        {
            await scoring.AddPointsAsync(guildId, KillPointDelta, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Four-guild point accrual for guild {GuildId} failed -- point lost, kill reward unaffected",
                guildId);
        }
    }

        public async Task RecomputeAsync(CancellationToken ct)
    {
        IReadOnlyList<GuildRankingRowDto> rows;
        try
        {
            rows = await scoring.GetLeaderboardAsync(LeaderboardSize, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Four-guild leaderboard recompute failed -- previous standings left in place");
            return;
        }

        var standings = new FourGuildStanding[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            standings[i] = new FourGuildStanding(rows[i].GuildId, rows[i].Name, rows[i].Points);

        _standings = standings;

        logger.LogDebug("Four-guild leaderboard recomputed: {Count} guild(s) with positive points", standings.Length);
    }
}
