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

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
