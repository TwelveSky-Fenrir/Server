using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface ICloseShopStallService
{
    public CloseShopStallResponse? CloseLiveShop(PlayerRuntimeState state);

    public ValueTask CloseOfflineShopAsync(int characterId, Zone zone, CancellationToken cancellationToken);
}
