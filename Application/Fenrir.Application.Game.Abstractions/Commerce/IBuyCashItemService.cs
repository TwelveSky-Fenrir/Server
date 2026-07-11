using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IBuyCashItemService
{

        public ValueTask<BuyCashItemResponse?> ResolveAndApplyAsync(BuyCashItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken);
}
