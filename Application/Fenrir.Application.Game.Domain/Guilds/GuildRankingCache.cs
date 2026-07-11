using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Guilds;

public sealed class GuildRankingCache
{

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

        public async Task RefreshAsync(IGuildRepository guilds, CancellationToken ct)
    {
        var top = await guilds.GetTopByPointsAsync(TopCount, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _top = [..top];
        }
    }
}
