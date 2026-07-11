using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Data.Guilds;

// Four-guild live-scoring data access (C17 Part B): enemy-tribe-kill point accrual (silent on unknown guild)
// and the top-N-positive leaderboard read. See game.usp_Guild_AddFourGuildPoints /
// game.usp_Guild_GetTopFourGuild headers for how these differ from the general guild-point surface.
public sealed record FourGuildScoringRepository(ICaeriusNetDbContext Db) : IFourGuildScoringRepository
{
    public async ValueTask AddPointsAsync(int guildId, int delta, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_AddFourGuildPoints", 0)
            .AddParameter("GuildId", guildId, SqlDbType.Int)
            .AddParameter("Delta", delta, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetLeaderboardAsync(int count,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Guild_GetTopFourGuild", count)
            .AddParameter("Count", count, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<GuildRankingRowDto>(sp, ct);
    }
}
