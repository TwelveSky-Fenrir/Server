using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.WarPoint;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.WarPoint;

public sealed class WarPointShopService(
    IWarPointRepository warPoints,
    IEventLogRepository eventLog,
    WarPointShopCatalog catalog,
    WorldDataCache worldData,
    ILogger<WarPointShopService> logger) : IWarPointShopService
{
    private const int WarPointBalanceUpdateSort = 905;

    private const int ContributionPointBalanceUpdateSort = 3;

    private const short WarPointShopBuyEventCode = 4;

    private const byte NpcShopTradeOutcome = 1;

    public async ValueTask<WarPointBuyServiceResult> TryBuyAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, int npcId, int itemId, int requestedQuantity, byte destinationPage, byte destinationSlot,
        CancellationToken ct)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
            return WarPointBuyServiceResult.NotHandled;

        var destination = state.Inventory.GetSlot(destinationPage, destinationSlot);
        var resolution = WarPointShopPolicy.ResolveBuy(catalog, npcId, itemDefinition, requestedQuantity, destination,
            state.ContributionPoints);

        switch (resolution.Outcome)
        {
            case WarPointShopPolicy.BuyOutcome.NotWarPointItem:
            case WarPointShopPolicy.BuyOutcome.PriceUnavailable:
                return WarPointBuyServiceResult.NotHandled;

            case WarPointShopPolicy.BuyOutcome.DestinationConflict:
                logger.LogInformation(
                    "Character {CharacterId} War-Point buy aborted: {Outcome} (NPC {NpcId}, item {ItemId})",
                    characterId, resolution.Outcome, npcId, itemId);
                return WarPointBuyServiceResult.Aborted;

            case WarPointShopPolicy.BuyOutcome.WrongNpcForItem:
            case WarPointShopPolicy.BuyOutcome.InvalidQuantity:
            case WarPointShopPolicy.BuyOutcome.InsufficientContributionPoints:
                logger.LogInformation(
                    "Character {CharacterId} War-Point buy soft-rejected: {Outcome} (NPC {NpcId}, item {ItemId})",
                    characterId, resolution.Outcome, npcId, itemId);
                return WarPointBuyServiceResult.SoftRejected;
        }

        var projected = state.Inventory.GetContainer(destinationPage)
            .SetItem(destinationSlot, resolution.NewDestinationStack!.Value);

        WarPointPurchaseResult purchase;
        try
        {
            purchase = await warPoints.BuyWarPointItemAsync(characterId, resolution.WarPointCost, destinationPage,
                ToTvps(projected), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} War-Point buy BuyWarPointItemAsync failed (item {ItemId})", characterId,
                itemId);
            return WarPointBuyServiceResult.Aborted;
        }

        if (!purchase.Purchased)
        {
            logger.LogInformation(
                "Character {CharacterId} War-Point buy soft-rejected: insufficient War-Points (item {ItemId}, cost {WarPointCost})",
                characterId, itemId, resolution.WarPointCost);
            return WarPointBuyServiceResult.SoftRejected;
        }

        var newWarPoint = purchase.NewWarPointBalance;
        var beforeWarPoint = newWarPoint + resolution.WarPointCost;
        var purchasedQuantity = resolution.NewDestinationStack!.Value.Quantity - (destination?.Quantity ?? 0);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(destinationPage, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null), ct))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped War-Point buy mirror for character {CharacterId} -- SQL is durable",
                zone.MapId, characterId);

        var newContributionPoints = state.ContributionPoints;
        if (resolution.ContributionPointCost > 0)
        {
            newContributionPoints = state.ContributionPoints - resolution.ContributionPointCost;
            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(characterId, newContributionPoints), ct))
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped War-Point CP mirror for character {CharacterId}",
                    zone.MapId, characterId);
        }

        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = WarPointBalanceUpdateSort, Value = newWarPoint, Value2 = 0 });

        if (resolution.ContributionPointCost > 0)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = ContributionPointBalanceUpdateSort, Value = newContributionPoints, Value2 = 0 });

        await eventLog.LogAsync(WarPointShopBuyEventCode, EventLogCategory.NpcShopTrade, accountId, characterId,
            null, null, null, null, null, itemId, purchasedQuantity, NpcShopTradeOutcome,
            $"WarPointBefore={beforeWarPoint};WarPointAfter={newWarPoint};WarPointCost={resolution.WarPointCost};CpCost={resolution.ContributionPointCost}",
            ct);

        logger.LogInformation(
            "Character {CharacterId} War-Point buy applied: item {ItemId} x{Quantity}, WP {Before}->{After}, CP -{CpCost}",
            characterId, itemId, purchasedQuantity, beforeWarPoint, newWarPoint, resolution.ContributionPointCost);

        return WarPointBuyServiceResult.Succeeded;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
