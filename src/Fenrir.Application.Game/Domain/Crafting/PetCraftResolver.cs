using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Crafting;

public static class PetCraftResolver
{
    public enum Outcome
    {
        Rejected,
        Success
    }

    private static readonly Result Rejected = new(Outcome.Rejected, 0, 0, 0, 0, 0, 0);

    public static Result ResolveRecipe0(ItemStack material1, ItemStack material2, ItemStack material3,
        ItemStack catalyst, IRandomSource random)
    {
        if (!AreAcceptedFusionMaterials(PetCraftRecipeCatalog.Recipe0FusionItemIds, material1.ItemId,
                material2.ItemId, material3.ItemId) || catalyst.ItemId != PetCraftRecipeCatalog.Recipe0CatalystItemId)
            return Rejected;

        var roll = random.NextInt32(100);
        return roll < 48
            ? Success(PetCraftRecipeCatalog.Recipe0ResultPool[roll / 8], 0, 0)
            : Success(PetCraftRecipeCatalog.FallbackItemId, PetCraftRecipeCatalog.Recipe0FallbackQuantity, 0);
    }

    public static Result ResolveRecipe1(ItemStack material1, ItemStack material2, ItemStack material3,
        ItemStack catalyst, IRandomSource random)
    {
        if (!AreAcceptedFusionMaterials(PetCraftRecipeCatalog.Recipe1FusionItemIds, material1.ItemId,
                material2.ItemId, material3.ItemId) || catalyst.ItemId != PetCraftRecipeCatalog.Recipe1CatalystItemId)
            return Rejected;

        var resultItemId = random.NextInt32(100) switch
        {
            < 1 => 1016,
            < 3 => 1012,
            < 6 => 1013,
            < 9 => 1014,
            < 12 => 1015,
            _ => PetCraftRecipeCatalog.FallbackItemId
        };
        var resultQuantity = resultItemId == PetCraftRecipeCatalog.FallbackItemId
            ? PetCraftRecipeCatalog.Recipe1FallbackQuantity
            : 0;
        return Success(resultItemId, resultQuantity, 0);
    }

    public static Result ResolveRecipe2(ItemStack material1, ItemStack material2, ItemStack material3,
        ItemStack catalyst)
    {
        if (material1.ItemId != PetCraftRecipeCatalog.Recipe2Material1ItemId ||
            material2.ItemId != PetCraftRecipeCatalog.Recipe2Material2ItemId ||
            material3.ItemId != PetCraftRecipeCatalog.Recipe2Material3ItemId ||
            catalyst.ItemId != PetCraftRecipeCatalog.Recipe2CatalystItemId)
            return Rejected;

        return Success(PetCraftRecipeCatalog.Recipe2ResultItemId, 0, 0);
    }

    public static Result ResolveRecipe3(ItemStack material1, ItemStack material2)
    {
        if (material1.ItemId != PetCraftRecipeCatalog.Recipe3MaterialItemId ||
            material2.ItemId != PetCraftRecipeCatalog.Recipe3MaterialItemId)
            return Rejected;

        return Success(PetCraftRecipeCatalog.Recipe3ResultItemId, 0, 0);
    }

    private static bool AreAcceptedFusionMaterials(IReadOnlySet<int> acceptedItemIds, int material1ItemId,
        int material2ItemId, int material3ItemId)
    {
        return acceptedItemIds.Contains(material1ItemId) && acceptedItemIds.Contains(material2ItemId) &&
               acceptedItemIds.Contains(material3ItemId);
    }

    private static Result Success(int resultItemId, int resultQuantity, int growthValue)
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(growthValue);
        return new Result(Outcome.Success, resultItemId, resultQuantity, enchant, combine, refine, socket);
    }

    public readonly record struct Result(
        Outcome Outcome,
        int ResultItemId,
        int ResultQuantity,
        byte Enchant,
        byte Combine,
        byte Refine,
        byte Socket)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
