using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     The shared "consume the used stack, persist it, mirror it onto the zone cache" tail every op23
///     <see cref="IUseItemHandler" /> ends with — the registry-side equivalent of
///     <c>UseInventoryItemService.ConsumeAndMirrorAsync</c>, so a handler never re-hand-rolls the
///     project/replace/mirror sequence (or the "SQL is durable, cache self-heals" full-inbox log line). The
///     durable SQL write happens first and is awaited; the in-memory mirror is best-effort (a dropped mirror
///     self-heals on the character's next world entry). Reference: Server/ts25zone/S04_MyWork03.cpp:613-628
///     (<c>DecreaseQunatity</c>) / :756-761 (the shared item-removal routine).
/// </summary>
public sealed class UseItemInventoryWriter(ICharacterRepository characters, ILogger<UseItemInventoryWriter> logger)
{
    /// <summary>
    ///     Writes the addressed slot down to <paramref name="remainingQuantity" /> (removing the slot outright
    ///     when that is zero), persists the whole page atomically, then mirrors the page (and any recomputed
    ///     stats) onto the zone. Returns the projected page content so a caller can assert on it if needed.
    /// </summary>
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

    /// <summary>
    ///     Atomically replaces two pages at once — the inventory page and the Equipment container for a
    ///     double-click-to-equip swap — then mirrors both (plus the recomputed stats) onto the zone. The two
    ///     writes MUST be one durable operation (a swap that half-committed would duplicate or destroy an
    ///     item), so this routes through <see cref="ICharacterRepository.ReplaceTwoContainersAsync" /> rather
    ///     than two independent <see cref="ICharacterRepository.ReplaceContainerAsync" /> round trips.
    /// </summary>
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

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, stats),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped op23 equip-swap mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>
    ///     Persists whichever of the two inventory pages a projection actually changed (one or both, atomically
    ///     when both) and mirrors them onto the zone -- the same "diff against the original, write only what
    ///     changed" shape <c>LootBoxUseItemHandler.PersistAndMirrorAsync</c> already established for the
    ///     box-open family, shared here so a second family that also nets a "consume one slot, grant into
    ///     another" projected-pages pair (e.g. <c>LuckyTicketUseItemHandler</c>, via
    ///     <c>LootBoxOpenResolver.OpenSingle</c>) does not need to re-hand-roll the same diff/atomic-write
    ///     decision. A no-op (no page differs from its original) is a silent return, since a caller only
    ///     reaches this after a reported success.
    /// </summary>
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
