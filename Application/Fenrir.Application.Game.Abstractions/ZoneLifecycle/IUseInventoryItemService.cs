using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>
///     Business logic for op23, CZ_USE_INVENTORY_ITEM_SEND -- three families out of the ~6300-line legacy switch
///     are modeled: the Bottle family (iSort==26, S04_MyWork03.cpp:2448, via
///     <see cref="BottleResolver.ResolveAcquire" />) and two members of the iSort==3 grab-bag of "right-click,
///     single-purpose" items -- see <c>UseInventoryItemHandler</c>'s own remarks for the full rationale.
/// </summary>
public interface IUseInventoryItemService
{
    public ValueTask<UseInventoryItemResponse> ResolveAsync(Zone zone, PlayerRuntimeState state, int characterId,
        byte page,
        byte index, CancellationToken cancellationToken);
}
