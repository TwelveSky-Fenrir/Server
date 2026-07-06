using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>
///     Business logic for op23, CZ_USE_INVENTORY_ITEM_SEND. Families modeled: Bottle (iSort==26,
///     S04_MyWork03.cpp:2448, via <see cref="BottleResolver.ResolveAcquire" />), GP ticket cash-shop-credit
///     (itemId 723/725), proxy-shop rental-extension (itemId 567/592/8422/8423), the loot-box family's LOD
///     round-ticket direct handler (itemId 1434), Stats Clear/Stat Cleanse, the banked protection/enhancement-
///     charge charm and scroll sub-groups, two unambiguous banked cash-timer ids (Faction Notice 566, Taiyan
///     Key 1049), and two members of the iSort==3 grab-bag of "right-click, single-purpose" items (Guild
///     Scroll, Faction Transfer Scroll) -- see <c>UseInventoryItemHandler</c>'s and
///     <c>UseInventoryItemService</c>'s own remarks for the full per-family rationale and citations.
/// </summary>
public interface IUseInventoryItemService
{
    /// <param name="value">
    ///     The wire request's multi-purpose numeric field -- bulk-use count for the loot-box/protection-charm/
    ///     stat-potion families (0/1 = plain click), the Stat Cleanse target-stat selector (1-4) for that
    ///     family, unused by every other family modeled so far.
    /// </param>
    public ValueTask<UseInventoryItemResponse> ResolveAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int accountId, byte page, byte index, int value, CancellationToken cancellationToken);
}
