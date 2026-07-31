using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface ISearchShopListingsService
{
    public ValueTask<IReadOnlyList<SearchShopListingsResponse>> SearchAsync(SearchShopListingsRequest packet,
        Zone zone, CancellationToken cancellationToken);
}
