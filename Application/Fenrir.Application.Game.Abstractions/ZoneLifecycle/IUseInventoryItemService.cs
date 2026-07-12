using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IUseInventoryItemService
{
    public ValueTask<UseInventoryItemResponse?> ResolveAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, byte page, byte index, int value, CancellationToken cancellationToken);
}
