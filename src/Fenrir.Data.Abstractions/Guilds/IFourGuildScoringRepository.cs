using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Guilds;

public interface IFourGuildScoringRepository
{
    public ValueTask AddPointsAsync(int guildId, int delta, CancellationToken ct);

    public ValueTask<ReadOnlyCollection<GuildRankingRowDto>> GetLeaderboardAsync(int count, CancellationToken ct);
}
