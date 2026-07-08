using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

/// <remarks>
///     Legacy replies to CZ_SET_DEPUTY_PSHOP_SEND (opcode 109) with one of ten distinct result codes, and
///     -- exactly like <see cref="OpenShopStallService" />'s already-preserved personal/proxy 0-vs-100 split
///     -- the *same* opcode uses two different success codes depending on which sub-operation ran: 0 for a
///     successful RETRIEVE, 1000 for a successful PURCHASE (contract: Shop Stalls and Proxy Shops,
///     UpdateProxyShop Outputs section; ServerDocs/12_ts25zone/15_MyGame08_09_EventsCenter_ProxyShops.md
///     §5.7 confirms <c>case 1</c> of <c>PROXY_SHOP_SYSTEM::Process</c>, Server/ts25zone/S07_MyGame09.cpp:557-884,
///     is where both sub-operations are dispatched). That success split is preserved below.
///     The remaining eight legacy failure codes are NOT itemized by the behavior contract or by
///     ServerDocs (both explicitly flag the retrieval mutation and the full result-code enumeration as "not
///     observed / needs a follow-up finding" before implementing them faithfully) -- per this project's "no
///     legacy parity from memory" rule, they are intentionally left collapsed to 1 (mismatch, unknown shop,
///     or any other RetrieveItemAndReplaceContainerAsync failure) / 2 (insufficient funds, a cap, or any
///     other ExecutePurchaseAsync failure) until a dedicated legacy-behavior-translator follow-up contract
///     supplies the full ten-code enumeration and the exact SQL error each stored procedure throws per case.
/// </remarks>
public sealed class UpdateProxyShopService(
    IOfflineShopRepository offlineShops,
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<UpdateProxyShopService> logger) : IUpdateProxyShopService
{
    /// <summary>
    ///     game.EventLog.EventCode for a proxy-shop retrieve row (legacy <c>GL_1001_PXSHOP_ITEM</c>, action
    ///     label "Retrieved"), scoped within <see cref="EventLogCategory.ProxyShop" /> -- see that enum
    ///     member's remarks for the full 1-4 numbering.
    /// </summary>
    private const short ProxyShopRetrieveEventCode = 2;

    /// <summary>Same legacy call site as <see cref="ProxyShopRetrieveEventCode" />, action label "Purchased".</summary>
    private const short ProxyShopPurchaseEventCode = 3;

    public UpdateProxyShopValidation Validate(UpdateProxyShopRequest packet)
    {
        if (packet.BuySort is not (1 or 2))
        {
            logger.LogDebug("Update proxy shop validation failed: invalid buySort {BuySort}", packet.BuySort);
            return new UpdateProxyShopValidation(true, 0, null);
        }

        var slotIndex = (short)(packet.SellPage * 5 + packet.SellIndex);
        if (packet.SellPage is < 0 or >= 5 || packet.SellIndex is < 0 or >= 5 ||
            packet.SelfPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.SelfPage, packet.SelfIndex) ||
            packet.SelfX is < 0 or > 7 || packet.SelfY is < 0 or > 7)
        {
            logger.LogDebug("Update proxy shop validation failed: out-of-range slot coordinates");
            return new UpdateProxyShopValidation(true, 0, null);
        }

        if (!worldData.ItemsById.TryGetValue(packet.SellItemIndex, out var itemDefinition))
        {
            logger.LogDebug("Update proxy shop validation failed: item {ItemId} is unresolvable",
                packet.SellItemIndex);
            return new UpdateProxyShopValidation(true, 0, null);
        }

        return new UpdateProxyShopValidation(false, slotIndex, itemDefinition);
    }

    public async ValueTask<UpdateProxyShopResponse?> RetrieveAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken)
    {
        var destination = state.Inventory.GetSlot((byte)packet.SelfPage, (byte)packet.SelfIndex);
        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        if (destination is { } existing &&
            (existing.ItemId != packet.SellItemIndex || !isStackable ||
             existing.Quantity + packet.Quantity > GroundItemPickupPolicy.MaxStackQuantity))
        {
            logger.LogWarning(
                "Offline-shop retrieve rejected: character {CharacterId} destination slot {SelfPage}/{SelfIndex} cannot accept item {ItemId} -- session will be disconnected",
                characterId, packet.SelfPage, packet.SelfIndex, packet.SellItemIndex);
            return null;
        }

        var finalQuantity = destination is { } d ? d.Quantity + packet.Quantity : packet.Quantity;
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(packet.Value);
        var newStack = new ItemStack(packet.SellItemIndex, finalQuantity, enchant, combine, refine, socket,
            packet.Socket[0], packet.Socket[1], packet.Socket[2], 0, packet.Serial);

        var projectedContainer = state.Inventory.GetContainer((byte)packet.SelfPage)
            .SetItem((byte)packet.SelfIndex, newStack);

        try
        {
            await offlineShops.RetrieveItemAndReplaceContainerAsync(characterId, slotIndex, packet.SellItemIndex,
                packet.Quantity, packet.Value, (byte)packet.SelfPage, ToTvps(projectedContainer), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} offline-shop retrieve RetrieveItemAndReplaceContainerAsync failed",
                characterId);
            return BuildReply(1, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, 0);
        }

        var response = BuildReply(0, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, 0);

        // Logged only once RetrieveItemAndReplaceContainerAsync above has durably committed. Money is
        // unconditionally 0 for a retrieve, matching legacy's own forced-zero before both the response and
        // the audit write (Server/ts25zone/S07_MyGame09.cpp:838-844) -- never packet.Price, which this branch
        // never even reads. The shop's own remaining Money/BigMoney are unaffected by a retrieve; re-read
        // fresh (rather than threaded from elsewhere) purely so this audit row reflects the actually-stored
        // balance, not an assumption. TargetAccountId/TargetCharacterId are left null: owner == actor here.
        var (shopAfterRetrieve, _) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        await eventLog.LogAsync(ProxyShopRetrieveEventCode, EventLogCategory.ProxyShop, accountId, characterId,
            null, null, null, 0, null, packet.SellItemIndex, packet.Quantity, 1,
            $"Action=Retrieved;Value={packet.Value};Serial={packet.Serial};Socket1={packet.Socket[0]};" +
            $"Socket2={packet.Socket[1]};Socket3={packet.Socket[2]};ShopOwnerName={state.Name};" +
            $"ShopMoneyAfter={shopAfterRetrieve?.Money ?? 0};ShopBigMoneyAfter={shopAfterRetrieve?.BigMoney ?? 0}",
            cancellationToken);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.SelfPage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop retrieve mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Offline-shop item retrieved: character {CharacterId} retrieved item {ItemId} x{Quantity} from their own closed shop",
            characterId, packet.SellItemIndex, packet.Quantity);

        return response;
    }

    public async ValueTask<UpdateProxyShopResponse?> PurchaseAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken)
    {
        var sellerId = await characters.GetIdByNameAsync(packet.AvatarName, cancellationToken);
        if (sellerId is null)
        {
            logger.LogDebug(
                "Offline-shop purchase rejected: character {CharacterId} seller {SellerAvatarName} does not exist",
                characterId, packet.AvatarName);
            return BuildReply(1, packet.SelfPage, packet.SelfIndex, null, packet.Socket, 0);
        }

        // Buying from one's own open shop would bypass RETRIEVE's "must be closed" gate and refund the
        // price into the shop's own earnings -- rejected as the safe, conservative choice.
        if (sellerId.Value == characterId)
        {
            logger.LogWarning(
                "Offline-shop purchase rejected: character {CharacterId} attempted to buy from its own proxy shop -- session will be disconnected",
                characterId);
            return null;
        }

        var destination = state.Inventory.GetSlot((byte)packet.SelfPage, (byte)packet.SelfIndex);
        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        if (destination is { } existing &&
            (existing.ItemId != packet.SellItemIndex || !isStackable ||
             existing.Quantity + packet.Quantity > GroundItemPickupPolicy.MaxStackQuantity))
        {
            logger.LogWarning(
                "Offline-shop purchase rejected: character {CharacterId} destination slot {SelfPage}/{SelfIndex} cannot accept item {ItemId} -- session will be disconnected",
                characterId, packet.SelfPage, packet.SelfIndex, packet.SellItemIndex);
            return null;
        }

        var finalQuantity = destination is { } d ? d.Quantity + packet.Quantity : packet.Quantity;
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(packet.Value);
        var newStack = new ItemStack(packet.SellItemIndex, finalQuantity, enchant, combine, refine, socket,
            packet.Socket[0], packet.Socket[1], packet.Socket[2], 0, packet.Serial);

        var projectedContainer = state.Inventory.GetContainer((byte)packet.SelfPage)
            .SetItem((byte)packet.SelfIndex, newStack);

        try
        {
            await offlineShops.ExecutePurchaseAsync(sellerId.Value, slotIndex, packet.SellItemIndex, packet.Quantity,
                packet.Value, packet.Price, characterId, (byte)packet.SelfPage, ToTvps(projectedContainer),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} offline-shop purchase ExecutePurchaseAsync failed",
                characterId);
            return BuildReply(2, packet.SelfPage, packet.SelfIndex, null, packet.Socket, 0);
        }

        // Result=1000 marks a successful PURCHASE, distinct from RETRIEVE's Result=0 above -- the same
        // legacy asymmetry OpenShopStall already preserves via its personal/proxy 0-vs-100 split. If the
        // legacy client branches its UI/state handling on the specific success code (as it does there), a
        // purchase misreported as Result=0 would be misinterpreted client-side as a retrieval.
        var response = BuildReply(1000, packet.SelfPage, packet.SelfIndex, newStack, packet.Socket, packet.Price);

        // Logged only once ExecutePurchaseAsync above has durably committed -- ShopMoneyAfter/BigMoneyAfter
        // re-read fresh from the seller's shop row so this audit row reflects the actual post-credit balance
        // (including any BigMoney rollover ExecutePurchaseAsync's own CASE WHEN applied), not a value
        // recomputed here that could drift from the stored procedure's own rounding/rollover logic.
        // TargetAccountId is deliberately left null: no cheap characterId->accountId lookup exists on
        // ICharacterRepository today, and the seller may be offline (the whole point of a proxy shop) so no
        // live PlayerRuntimeState is available either -- TargetCharacterId is still populated, and
        // game.Characters.AccountId is trivially joinable from it for any downstream audit query.
        var (shopAfterPurchase, _) = await offlineShops.GetByCharacterAsync(sellerId.Value, cancellationToken);
        await eventLog.LogAsync(ProxyShopPurchaseEventCode, EventLogCategory.ProxyShop, accountId, characterId,
            null, sellerId.Value, null, packet.Price, null, packet.SellItemIndex, packet.Quantity, 1,
            $"Action=Purchased;Value={packet.Value};Serial={packet.Serial};Socket1={packet.Socket[0]};" +
            $"Socket2={packet.Socket[1]};Socket3={packet.Socket[2]};ShopOwnerName={packet.AvatarName};" +
            $"ShopMoneyAfter={shopAfterPurchase?.Money ?? 0};ShopBigMoneyAfter={shopAfterPurchase?.BigMoney ?? 0}",
            cancellationToken);

        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot((byte)packet.SelfPage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped offline-shop purchase mirror for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Offline-shop purchase completed: buyer {BuyerId} bought item {ItemId} x{Quantity} from seller {SellerId} for {Price}",
            characterId, packet.SellItemIndex, packet.Quantity, sellerId.Value, packet.Price);

        return response;
    }

    private static UpdateProxyShopResponse BuildReply(int result, int page, int index, ItemStack? stack,
        int[] requestSocket, int money)
    {
        var value1 = stack is { } s
            ?
            [
                s.ItemId, 0, 0, s.Quantity, ItemValueCodec.Encode(s.Enchant, s.Combine, s.Refine, s.Socket), s.Serial,
                requestSocket[0], requestSocket[1], requestSocket[2]
            ]
            : new int[9];

        return new UpdateProxyShopResponse
        {
            Result = result,
            ProxyUser = ProxyShopWireMapper.Build(string.Empty, null, []),
            Page = page,
            Index = index,
            Value1 = value1,
            Money = money
        };
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
