using Fenrir.Application.Game.Abstractions.WarPoint;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.WarPoint;

public sealed class WarPointShopService(
    WarPointShopCatalog catalog,
    WorldDataCache worldData,
    ILogger<WarPointShopService> logger) : IWarPointShopService
{
    public ValueTask<WarPointBuyServiceResult> TryBuyAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, int npcId, int itemId, int requestedQuantity, byte destinationPage, byte destinationSlot,
        byte destinationX, byte destinationY, CancellationToken ct)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
            return ValueTask.FromResult(WarPointBuyServiceResult.NotHandled);

        var destination = state.Inventory.GetSlot(destinationPage, destinationSlot);
        var resolution = WarPointShopPolicy.ResolveBuy(catalog, npcId, itemDefinition, requestedQuantity, destination,
            destinationX, destinationY, state.ContributionPoints);

        switch (resolution.Outcome)
        {
            case WarPointShopPolicy.BuyOutcome.NotWarPointItem:
                return ValueTask.FromResult(WarPointBuyServiceResult.NotHandled);

            case WarPointShopPolicy.BuyOutcome.PriceUnavailable:

            case WarPointShopPolicy.BuyOutcome.DestinationConflict:
                logger.LogInformation(
                    "Character {CharacterId} War-Point buy aborted: {Outcome} (NPC {NpcId}, item {ItemId})",
                    characterId, resolution.Outcome, npcId, itemId);
                return ValueTask.FromResult(WarPointBuyServiceResult.Aborted);

            case WarPointShopPolicy.BuyOutcome.WrongNpcForItem:
            case WarPointShopPolicy.BuyOutcome.InvalidQuantity:
            case WarPointShopPolicy.BuyOutcome.InsufficientContributionPoints:
                logger.LogInformation(
                    "Character {CharacterId} War-Point buy soft-rejected: {Outcome} (NPC {NpcId}, item {ItemId})",
                    characterId, resolution.Outcome, npcId, itemId);
                return ValueTask.FromResult(WarPointBuyServiceResult.SoftRejected);
        }

        if (!HasAuthoritativeNonNegativeAmounts(catalog, itemDefinition, requestedQuantity, resolution) ||
            state.WarPoint < 0 || state.ContributionPoints < 0)
        {
            logger.LogWarning(
                "Character {CharacterId} War-Point buy rejected: invalid authoritative amount or negative in-memory balance (NPC {NpcId}, item {ItemId})",
                characterId, npcId, itemId);
            return ValueTask.FromResult(WarPointBuyServiceResult.SoftRejected);
        }

        logger.LogWarning(
            "Character {CharacterId} War-Point buy backpressured: IWarPointRepository lacks an idempotent purchase transaction (NPC {NpcId}, item {ItemId})",
            characterId, npcId, itemId);
        return ValueTask.FromResult(WarPointBuyServiceResult.SoftRejected);
    }

    private static bool HasAuthoritativeNonNegativeAmounts(WarPointShopCatalog catalog, ItemDefinition itemDefinition,
        int requestedQuantity, WarPointShopPolicy.BuyResolution resolution)
    {
        if (!catalog.TryGetPrice(itemDefinition.Item.ItemId, out var price))
            return false;

        var effectiveQuantity = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort) ? requestedQuantity : 1;
        if (effectiveQuantity < 1 || price.WarPointPrice < 0 || price.ContributionPointPrice < 0)
            return false;

        try
        {
            var expectedWarPointCost = checked(price.WarPointPrice * effectiveQuantity);
            var expectedContributionPointCost = checked(price.ContributionPointPrice * effectiveQuantity);
            return resolution.WarPointCost == expectedWarPointCost &&
                   resolution.ContributionPointCost == expectedContributionPointCost;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
