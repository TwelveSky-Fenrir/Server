using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Progression;

/// <summary><see langword="null" /> per period means that period's throttle has not elapsed yet -- no reply for it.</summary>
public readonly record struct HeroRankingQueryResult(HeroRank? Previous, HeroRank? Current);

public interface IHeroRankingService
{
    public ValueTask<HeroRankingQueryResult> QueryAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}
