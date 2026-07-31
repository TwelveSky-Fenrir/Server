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

    public static Result ResolveRecipe1(ItemStack material1, ItemStack material2, ItemStack material3,
        ItemStack catalyst, IRandomSource random)
    {
        if (!PetCraftRecipeCatalog.Recipe1FusionItemIds.Contains(material1.ItemId) ||
            !PetCraftRecipeCatalog.Recipe1FusionItemIds.Contains(material2.ItemId) ||
            !PetCraftRecipeCatalog.Recipe1FusionItemIds.Contains(material3.ItemId) ||
            catalyst.ItemId != PetCraftRecipeCatalog.Recipe1CatalystItemId)
            return Rejected;

        var roll = random.NextInt32(100);
        return roll < 48
            ? Success(PetCraftRecipeCatalog.Recipe1ResultPool[roll / 8], 0, 0)
            : Success(PetCraftRecipeCatalog.ConsolationItemId, PetCraftRecipeCatalog.Recipe1ConsolationQuantity, 0);
    }

    public static Result ResolveRecipe2(ItemStack material1, ItemStack material2, ItemStack material3,
        ItemStack catalyst, IRandomSource random)
    {
        if (!PetCraftRecipeCatalog.Recipe2FusionItemIds.Contains(material1.ItemId) ||
            !PetCraftRecipeCatalog.Recipe2FusionItemIds.Contains(material2.ItemId) ||
            !PetCraftRecipeCatalog.Recipe2FusionItemIds.Contains(material3.ItemId) ||
            catalyst.ItemId != PetCraftRecipeCatalog.Recipe2CatalystItemId)
            return Rejected;

        var roll = random.NextInt32(100);
        var resultItemId = roll switch
        {
            < 1 => 1016,
            < 3 => 1012,
            < 6 => 1013,
            < 9 => 1014,
            < 12 => 1015,
            _ => 0
        };

        if (resultItemId == 0)
            return Success(PetCraftRecipeCatalog.ConsolationItemId, PetCraftRecipeCatalog.Recipe2ConsolationQuantity,
                0);

        return Success(resultItemId, 0, 0);
    }

    public static Result ResolveRecipe3(ItemStack material1, ItemStack material2, ItemStack material3,
        ItemStack catalyst)
    {
        if (material1.ItemId != PetCraftRecipeCatalog.Recipe3Material1ItemId ||
            material2.ItemId != PetCraftRecipeCatalog.Recipe3Material2ItemId ||
            material3.ItemId != PetCraftRecipeCatalog.Recipe3Material3ItemId ||
            catalyst.ItemId != PetCraftRecipeCatalog.Recipe3CatalystItemId)
            return Rejected;

        return Success(PetCraftRecipeCatalog.Recipe3ResultItemId, 0, 0);
    }

    public static Result ResolveRecipe4(ItemStack material1, ItemStack material2)
    {
        if (material1.ItemId != PetCraftRecipeCatalog.Recipe4MaterialItemId ||
            material2.ItemId != PetCraftRecipeCatalog.Recipe4MaterialItemId)
            return Rejected;

        return Success(PetCraftRecipeCatalog.Recipe4ResultItemId, 0, 0);
    }

    public static Result ResolveRecipe5(ItemStack material1, ItemStack material2, IRandomSource random)
    {
        if (material1.ItemId != PetCraftRecipeCatalog.Recipe5Material1ItemId ||
            material2.ItemId != PetCraftRecipeCatalog.Recipe5Material2ItemId ||
            GrowthValueOf(material1) < PetCraftRecipeCatalog.GodRecipeMaterial1GrowthThreshold ||
            GrowthValueOf(material2) < PetCraftRecipeCatalog.GodRecipeMaterial2GrowthThreshold)
            return Rejected;

        var resultItemId = PetCraftRecipeCatalog.Recipe5ResultPool[random.NextInt32(3)];
        return Success(resultItemId, 0, PetCraftRecipeCatalog.GodRecipeResultGrowthValue);
    }

    public static Result ResolveRecipe6(ItemStack material1, ItemStack material2, IRandomSource random)
    {
        if (material1.ItemId != PetCraftRecipeCatalog.Recipe6Material1ItemId ||
            material2.ItemId != PetCraftRecipeCatalog.Recipe6Material2ItemId ||
            GrowthValueOf(material1) < PetCraftRecipeCatalog.GodRecipeMaterial1GrowthThreshold ||
            GrowthValueOf(material2) < PetCraftRecipeCatalog.GodRecipeMaterial2GrowthThreshold)
            return Rejected;

        var resultItemId = PetCraftRecipeCatalog.Recipe6ResultPool[random.NextInt32(2)];
        return Success(resultItemId, 0, PetCraftRecipeCatalog.GodRecipeResultGrowthValue);
    }

    private static int GrowthValueOf(ItemStack item)
    {
        return ItemValueCodec.Encode(item.Enchant, item.Combine, item.Refine, item.Socket);
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
