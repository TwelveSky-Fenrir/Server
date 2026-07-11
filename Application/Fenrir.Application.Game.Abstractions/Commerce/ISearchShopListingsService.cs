using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface ISearchShopListingsService
{

        public ValueTask<IReadOnlyList<SearchShopListingsResponse>> SearchAsync(SearchShopListingsRequest packet,
        Zone zone, CancellationToken cancellationToken);
}
