using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface ICloseShopStallService
{
    public ValueTask CloseLiveShopAsync(PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);

    public ValueTask CloseOfflineShopAsync(int characterId, Zone zone, CancellationToken cancellationToken);
}
