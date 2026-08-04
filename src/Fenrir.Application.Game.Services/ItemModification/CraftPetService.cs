using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class CraftPetService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    WorldDataCache worldData,
    ILogger<CraftPetService> logger)
    : ICraftPetService
{
    private const short FourSlotRecipeEventCode = 1;
    private const short TwoSlotRecipeEventCode = 2;

    public async ValueTask<CraftPetResult> ResolveFourSlotRecipeAsync(CraftPetRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (packet.Sort is < PetCraftRecipeCatalog.Recipe0Sort or > PetCraftRecipeCatalog.Recipe2Sort)
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        if (!IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2) ||
            !IsValidSlot(packet.Page3, packet.Index3) || !IsValidSlot(packet.Page4, packet.Index4) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2, packet.Page3,
                packet.Index3, packet.Page4, packet.Index4))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var today = GameDate.Today();
        if (!RentedInventoryPageGate.IsPageAccessible(packet.Page1, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(packet.Page2, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(packet.Page3, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(packet.Page4, state.InventoryDate, today))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var material3 = state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3);
        var catalyst = state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4);

        if (material1 is not { } m1 || material2 is not { } m2 || material3 is not { } m3 ||
            catalyst is not { } cat || !IsWholeSlotIngredient(m1) || !IsWholeSlotIngredient(m2) ||
            !IsWholeSlotIngredient(m3) || !HasValidStoredQuantity(cat))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        PetCraftResolver.Result resolved;
        switch (packet.Sort)
        {
            case PetCraftRecipeCatalog.Recipe0Sort:
                resolved = PetCraftResolver.ResolveRecipe0(m1, m2, m3, cat, SystemRandomSource.Instance);
                break;
            case PetCraftRecipeCatalog.Recipe1Sort:
                resolved = PetCraftResolver.ResolveRecipe1(m1, m2, m3, cat, SystemRandomSource.Instance);
                break;
            case PetCraftRecipeCatalog.Recipe2Sort:
                resolved = PetCraftResolver.ResolveRecipe2(m1, m2, m3, cat);
                break;
            default:
                return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);
        }

        if (!resolved.Succeeded)
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        if (!TryCreateResultPet(m1, resolved, out var newPet))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, newPet);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);

        var remainingCatalystQuantity = cat.Quantity - 1;
        working[(byte)packet.Page4] = remainingCatalystQuantity > 0
            ? working[(byte)packet.Page4]
                .SetItem((byte)packet.Index4, cat with { Quantity = remainingCatalystQuantity })
            : working[(byte)packet.Page4].Remove((byte)packet.Index4);

        return await PersistAndBuildResultAsync(zone, state, characterId, accountId, FourSlotRecipeEventCode,
            working, resolved, newPet, 10000, RecipeLabel(packet.Sort), cancellationToken);
    }

    public async ValueTask<CraftPetResult> ResolveTwoSlotRecipeAsync(CraftPetRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (packet.Sort != PetCraftRecipeCatalog.Recipe3Sort ||
            !IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var today = GameDate.Today();
        if (!RentedInventoryPageGate.IsPageAccessible(packet.Page1, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(packet.Page2, state.InventoryDate, today))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        if (material1 is not { } m1 || material2 is not { } m2 ||
            !IsWholeSlotIngredient(m1) || !IsWholeSlotIngredient(m2))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var resolved = PetCraftResolver.ResolveRecipe3(m1, m2);
        if (!resolved.Succeeded || !TryCreateResultPet(m1, resolved, out var newPet))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, newPet);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);

        return await PersistAndBuildResultAsync(zone, state, characterId, accountId, TwoSlotRecipeEventCode,
            working, resolved, newPet, 0, RecipeLabel(packet.Sort), cancellationToken);
    }

    private static string RecipeLabel(int sort)
    {
        return $"pet-recipe-{sort}";
    }

    private async ValueTask<CraftPetResult> PersistAndBuildResultAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int accountId, short eventCode,
        Dictionary<byte, ImmutableDictionary<byte, ItemStack>> working,
        PetCraftResolver.Result resolved, ItemStack newPet, int wireResult, string recipeLabel,
        CancellationToken cancellationToken)
    {
        var pages = working.Keys.ToArray();
        if (pages.Length == 1)
            await characters.ReplaceContainerAsync(characterId, pages[0], ToTvps(working[pages[0]]),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, pages[0], ToTvps(working[pages[0]]), pages[1],
                ToTvps(working[pages[1]]), cancellationToken);

        await eventLog.LogAsync(eventCode, EventLogCategory.ItemCreate, accountId, characterId, null, null, null,
            null, null, resolved.ResultItemId, Math.Max(newPet.Quantity, 1), 1, null, cancellationToken);

        var containers = pages.Select(page => new InventoryContainerSnapshot(page, working[page]))
            .ToImmutableArray();
        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, containers, null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft-pet mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);
        }

        CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resolved.ResultItemId,
            recipeLabel);

        return new CraftPetResult(CraftPetOutcome.Applied, wireResult, resolved.ResultItemId,
            newPet.Quantity, newPet.Serial);
    }

    private static void EnsureContainer(Dictionary<byte, ImmutableDictionary<byte, ItemStack>> working,
        PlayerRuntimeState state, byte page)
    {
        if (!working.ContainsKey(page))
            working[page] = state.Inventory.GetContainer(page);
    }

    private static bool IsValidSlot(int page, int index)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, index);
    }

    private bool HasValidStoredQuantity(ItemStack stack)
    {
        return worldData.ItemsById.TryGetValue(stack.ItemId, out var definition) &&
               ItemQuantityPolicy.IsWithinLegalRange(definition.Item.Sort, stack.Quantity);
    }

    private bool IsWholeSlotIngredient(ItemStack stack)
    {
        return worldData.ItemsById.TryGetValue(stack.ItemId, out var definition) &&
               ItemQuantityPolicy.IsWithinLegalRange(definition.Item.Sort, stack.Quantity) &&
               (!ItemQuantityPolicy.IsStackableSort(definition.Item.Sort) ||
                stack.Quantity == ItemQuantityPolicy.MinStackQuantity);
    }

    private bool TryCreateResultPet(ItemStack material, PetCraftResolver.Result resolved, out ItemStack result)
    {
        if (!worldData.ItemsById.TryGetValue(resolved.ResultItemId, out var definition))
        {
            result = default;
            return false;
        }

        var quantity = ItemQuantityPolicy.IsStackableSort(definition.Item.Sort)
            ? resolved.ResultQuantity > 0 ? resolved.ResultQuantity : ItemQuantityPolicy.MinStackQuantity
            : resolved.ResultQuantity;
        if (!ItemQuantityPolicy.IsWithinLegalRange(definition.Item.Sort, quantity))
        {
            result = default;
            return false;
        }

        result = material with
        {
            ItemId = resolved.ResultItemId, Quantity = quantity, Enchant = resolved.Enchant,
            Combine = resolved.Combine, Refine = resolved.Refine, Socket = resolved.Socket
        };
        return true;
    }

    private static bool AreDistinctSlots(int page1, int index1, int page2, int index2, int page3, int index3,
        int page4, int index4)
    {
        return (page1 != page2 || index1 != index2) &&
               (page1 != page3 || index1 != index3) &&
               (page1 != page4 || index1 != index4) &&
               (page2 != page3 || index2 != index3) &&
               (page2 != page4 || index2 != index4) &&
               (page3 != page4 || index3 != index4);
    }

    private static bool AreDistinctSlots(int page1, int index1, int page2, int index2)
    {
        return page1 != page2 || index1 != index2;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
