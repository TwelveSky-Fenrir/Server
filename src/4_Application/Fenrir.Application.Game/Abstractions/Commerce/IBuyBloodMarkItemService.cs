using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IBuyBloodMarkItemService
{
    public ValueTask<BuyBloodMarkItemResponse?> ResolveAndApplyAsync(BuyBloodMarkItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);
}
