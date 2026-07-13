using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class CraftLegendaryPetService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<CraftLegendaryPetService> logger)
    : ICraftLegendaryPetService
{
    private const short LegendaryPetCraftEventCode = 1;

    public async ValueTask<CraftLegendaryPetResult> ResolveAsync(CraftLegendaryPetRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (packet.Sort != LegendaryPetCraftCatalog.Sort ||
            !IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2) ||
            !IsValidSlot(packet.Page3, packet.Index3))
            return new CraftLegendaryPetResult(CraftLegendaryPetOutcome.Rejected, 0, 0);

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var material3 = state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3);

        if (material1 is not { } m1 || material2 is not { } m2 || material3 is not { } m3 ||
            !worldData.ItemsById.TryGetValue(m1.ItemId, out var material1Definition))
            return new CraftLegendaryPetResult(CraftLegendaryPetOutcome.Rejected, 0, 0);

        var resolved = LegendaryPetCraftResolver.Resolve(material1Definition.Item.Sort, m2.ItemId, m3.ItemId,
            state.ContributionPoints, SystemRandomSource.Instance);

        if (!resolved.Succeeded)
            return new CraftLegendaryPetResult(CraftLegendaryPetOutcome.Rejected, 0, 0);

        var newPet = m1 with
        {
            ItemId = resolved.ResultItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
        };

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, newPet);

        var remainingMaterial2Quantity = m2.Quantity - 1;
        working[(byte)packet.Page2] = remainingMaterial2Quantity > 0
            ? working[(byte)packet.Page2]
                .SetItem((byte)packet.Index2, m2 with { Quantity = remainingMaterial2Quantity })
            : working[(byte)packet.Page2].Remove((byte)packet.Index2);

        var remainingMaterial3Quantity = m3.Quantity - 1;
        working[(byte)packet.Page3] = remainingMaterial3Quantity > 0
            ? working[(byte)packet.Page3]
                .SetItem((byte)packet.Index3, m3 with { Quantity = remainingMaterial3Quantity })
            : working[(byte)packet.Page3].Remove((byte)packet.Index3);

        var pages = working.Keys.ToArray();
        if (pages.Length == 1)
            await characters.ReplaceContainerAsync(characterId, pages[0], ToTvps(working[pages[0]]),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, pages[0], ToTvps(working[pages[0]]), pages[1],
                ToTvps(working[pages[1]]), cancellationToken);

        await eventLog.LogAsync(LegendaryPetCraftEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resolved.ResultItemId, 1, 1, null, cancellationToken);

        var containers = pages.Select(page => new InventoryContainerSnapshot(page, working[page]))
            .ToImmutableArray();
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft-legendary-pet inventory mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId,
                    state.ContributionPoints - LegendaryPetCraftCatalog.ContributionPointCost), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped CP mirror for character {CharacterId} after craft-legendary-pet",
                zone.MapId, characterId);

        return new CraftLegendaryPetResult(CraftLegendaryPetOutcome.Applied, resolved.ResultItemId, newPet.Serial);
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

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
