using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class CraftSkillBookService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<CraftSkillBookService> logger)
    : ICraftSkillBookService
{
    public async ValueTask<CraftSkillBookResult> ResolveAsync(CraftSkillBookRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (packet.Sort is < SkillBookCraftCatalog.Recipe1Sort or > SkillBookCraftCatalog.Recipe3Sort)
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);

        if (!IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2) ||
            !IsValidSlot(packet.Page3, packet.Index3) || !IsValidSlot(packet.Page4, packet.Index4) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2, packet.Page3,
                packet.Index3, packet.Page4, packet.Index4))
        {
            logger.LogDebug("Character {CharacterId} craft-skill-book rejected: invalid slot(s)", characterId);
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);
        }

        var today = GameDate.Today();
        if (!RentedInventoryPageGate.IsPageAccessible(packet.Page1, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(packet.Page2, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(packet.Page3, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(packet.Page4, state.InventoryDate, today))
        {
            logger.LogDebug("Character {CharacterId} craft-skill-book rejected: rented inventory page expired",
                characterId);
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);
        }

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var material3 = state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3);
        var material4 = state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4);

        if (material1 is not { } m1 || material2 is not { } m2 || material3 is not { } m3 ||
            material4 is not { } m4 || !IsWholeSlotIngredient(m1) || !IsWholeSlotIngredient(m2) ||
            !IsWholeSlotIngredient(m3) || !IsWholeSlotIngredient(m4))
        {
            logger.LogDebug(
                "Character {CharacterId} craft-skill-book rejected: one or more material slots empty", characterId);
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);
        }

        var resolved = SkillBookCraftResolver.ResolveFragments(packet.Sort, m1.ItemId, m2.ItemId, m3.ItemId,
            m4.ItemId);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft-skill-book rejected by resolver (sort {Sort}, materials {M1}/{M2}/{M3}/{M4})",
                characterId, packet.Sort, m1.ItemId, m2.ItemId, m3.ItemId, m4.ItemId);
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);
        }

        if (!TryCreateResultBook(m1, resolved.ResultItemId, out var newBook))
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, newBook);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);
        working[(byte)packet.Page4] = working[(byte)packet.Page4].Remove((byte)packet.Index4);

        var pages = working.Keys.ToArray();
        if (pages.Length == 1)
            await characters.ReplaceContainerAsync(characterId, pages[0], ToTvps(working[pages[0]]),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, pages[0], ToTvps(working[pages[0]]), pages[1],
                ToTvps(working[pages[1]]), cancellationToken);

        var containers = pages.Select(page => new InventoryContainerSnapshot(page, working[page]))
            .ToImmutableArray();
        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, containers, null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft-skill-book mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);
        }

        logger.LogInformation(
            "Character {CharacterId} craft-skill-book applied: result item {ResultItemId}", characterId,
            resolved.ResultItemId);

        return new CraftSkillBookResult(CraftSkillBookOutcome.Applied, resolved.ResultItemId, newBook.Serial);
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

    private bool TryCreateResultBook(ItemStack material, int resultItemId, out ItemStack result)
    {
        if (!worldData.ItemsById.TryGetValue(resultItemId, out var definition))
        {
            result = default;
            return false;
        }

        var quantity = ItemQuantityPolicy.IsStackableSort(definition.Item.Sort)
            ? ItemQuantityPolicy.MinStackQuantity
            : 0;
        if (!ItemQuantityPolicy.IsWithinLegalRange(definition.Item.Sort, quantity))
        {
            result = default;
            return false;
        }

        result = material with
        {
            ItemId = resultItemId, Quantity = quantity, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
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

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
