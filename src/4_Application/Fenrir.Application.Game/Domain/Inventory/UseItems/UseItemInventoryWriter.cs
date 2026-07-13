using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public sealed class UseItemInventoryWriter(ICharacterRepository characters, ILogger<UseItemInventoryWriter> logger)
{
    public async ValueTask<ImmutableDictionary<byte, ItemStack>> ConsumeAndMirrorAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item, int remainingQuantity,
        EffectiveStats? stats, CancellationToken cancellationToken)
    {
        var container = state.Inventory.GetContainer(page);
        var projected = remainingQuantity > 0
            ? container.SetItem(index, item with { Quantity = remainingQuantity })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, stats),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped op23 use-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return projected;
    }

    public async ValueTask ReplaceTwoAndMirrorAsync(Zone zone, int characterId, byte pageContainer,
        ImmutableDictionary<byte, ItemStack> pageProjected, byte equipmentContainer,
        ImmutableDictionary<byte, ItemStack> equipmentProjected, EffectiveStats? stats,
        CancellationToken cancellationToken)
    {
        await characters.ReplaceTwoContainersAsync(characterId, pageContainer, ToTvps(pageProjected),
            equipmentContainer, ToTvps(equipmentProjected), cancellationToken);

        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(pageContainer, pageProjected),
            new InventoryContainerSnapshot(equipmentContainer, equipmentProjected));

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, containers, stats, RecomputeCombatPoseAfterEquip: true),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped op23 equip-swap mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    public async ValueTask ReplaceProjectedPagesAndMirrorAsync(Zone zone, int characterId,
        ImmutableDictionary<byte, ItemStack> originalPage0, ImmutableDictionary<byte, ItemStack> originalPage1,
        ImmutableDictionary<byte, ItemStack> projectedPage0, ImmutableDictionary<byte, ItemStack> projectedPage1,
        EffectiveStats? stats, CancellationToken cancellationToken)
    {
        var page0Changed = !ReferenceEquals(projectedPage0, originalPage0);
        var page1Changed = !ReferenceEquals(projectedPage1, originalPage1);

        ImmutableArray<InventoryContainerSnapshot> containers;
        if (page0Changed && page1Changed)
        {
            await characters.ReplaceTwoContainersAsync(characterId,
                ContainerMatrix.InventoryPage0, ToTvps(projectedPage0),
                ContainerMatrix.InventoryPage1, ToTvps(projectedPage1), cancellationToken);
            containers = ImmutableArray.Create(
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, projectedPage0),
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, projectedPage1));
        }
        else if (page0Changed || page1Changed)
        {
            var container = page1Changed ? ContainerMatrix.InventoryPage1 : ContainerMatrix.InventoryPage0;
            var projected = page1Changed ? projectedPage1 : projectedPage0;
            await characters.ReplaceContainerAsync(characterId, container, ToTvps(projected), cancellationToken);
            containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));
        }
        else
        {
            return;
        }

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, stats),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped op23 use-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
