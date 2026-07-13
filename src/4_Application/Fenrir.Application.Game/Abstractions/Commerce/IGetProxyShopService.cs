using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IGetProxyShopService
{
    public ValueTask<GetProxyShopResponse> GetAsync(GetProxyShopRequest packet, Zone zone, int characterId,
        CancellationToken cancellationToken);
}
