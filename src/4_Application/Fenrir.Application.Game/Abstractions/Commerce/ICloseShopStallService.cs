using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface ICloseShopStallService
{
    public CloseShopStallResponse? CloseLiveShop(PlayerRuntimeState state);

    public ValueTask CloseOfflineShopAsync(int characterId, Zone zone, CancellationToken cancellationToken);
}
