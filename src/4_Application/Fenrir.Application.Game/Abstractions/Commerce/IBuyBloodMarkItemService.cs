using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IBuyBloodMarkItemService
{
    public ValueTask<BuyBloodMarkItemResponse?> ResolveAndApplyAsync(BuyBloodMarkItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}
