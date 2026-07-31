using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Progression;

public readonly record struct HeroRankingQueryResult(HeroRank? Previous, HeroRank? Current);

public interface IHeroRankingService
{
    public ValueTask<HeroRankingQueryResult> QueryAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
