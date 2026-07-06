using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op29, CZ_MAKE_ITEM_SEND -- extracted from <see cref="CraftItemHandler" />, see that
///     handler's remarks.
/// </summary>
public sealed class CraftItemService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<CraftItemService> logger)
    : ICraftItemService
{
    /// <summary>
    ///     game.EventLog.EventCode for a Jade Upgrade craft (MK_* sort <see cref="CraftRecipeCatalog.JadeUpgradeSort" />)
    ///     minting a Red Jade -- scoped independently within <see cref="EventLogCategory.ItemCreate" />; EventCode
    ///     is only ever caller-interpreted alongside its Category (see game.EventLog.sql's own "app-owned
    ///     numbering scheme" comment), so this does not collide with any other family's numbering, including
    ///     <see cref="AdvancedElixirEventCode" /> below.
    /// </summary>
    private const short JadeUpgradeEventCode = 1;

    /// <summary>
    ///     game.EventLog.EventCode for an Advanced Elixir craft (MK_* sort
    ///     <see cref="CraftRecipeCatalog.AdvancedElixirSort" />) minting a random [801,806] item, scoped
    ///     independently within <see cref="EventLogCategory.ItemCreate" />.
    /// </summary>
    private const short AdvancedElixirEventCode = 2;

    private static readonly byte[] InventoryPagesInScanOrder =
        [ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1];

    public async ValueTask<JadeUpgradeResult> ResolveJadeUpgradeAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (!IsValidInventorySlot(page1, index1) || !IsValidInventorySlot(page2, index2))
            return new JadeUpgradeResult(JadeUpgradeOutcome.Rejected, 0, 0);

        var material1 = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var material2 = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (material1 is not { } m1 || material2 is not { } m2)
            return new JadeUpgradeResult(JadeUpgradeOutcome.Rejected, 0, 0);

        var resolved = CraftResolver.ResolveJadeUpgrade(m1, m2);
        if (!resolved.Succeeded)
            return new JadeUpgradeResult(JadeUpgradeOutcome.Rejected, 0, 0);

        var result = resolved.ResultStack!.Value;

        ImmutableDictionary<byte, ItemStack> projected1;
        ImmutableDictionary<byte, ItemStack> projected2;

        if (page1 == page2)
        {
            var combined = state.Inventory.GetContainer((byte)page1)
                .SetItem((byte)index1, result)
                .Remove((byte)index2);
            projected1 = combined;
            projected2 = combined;

            await characters.ReplaceContainerAsync(characterId, (byte)page1, ToTvps(projected1), cancellationToken);
        }
        else
        {
            projected1 = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, result);
            projected2 = state.Inventory.GetContainer((byte)page2).Remove((byte)index2);

            await characters.ReplaceTwoContainersAsync(characterId, (byte)page1, ToTvps(projected1), (byte)page2,
                ToTvps(projected2), cancellationToken);
        }

        // Logged only once the container replace(s) above have durably committed -- an ItemCreate row must
        // never assert a mint that the DB write didn't actually persist.
        await eventLog.LogAsync(JadeUpgradeEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, result.ItemId, result.Quantity, 1, null, cancellationToken);

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected1))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projected1),
                new InventoryContainerSnapshot((byte)page2, projected2));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (jade) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new JadeUpgradeResult(JadeUpgradeOutcome.Applied, result.ItemId, result.Serial);
    }

    public async ValueTask<AdvancedElixirResult> ResolveAdvancedElixirAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (!IsValidInventorySlot(page1, index1))
            return new AdvancedElixirResult(AdvancedElixirOutcome.Rejected, null, 0, 0, null);

        var materialStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (materialStack is not { } material)
            return new AdvancedElixirResult(AdvancedElixirOutcome.Rejected, null, 0, 0, null);

        // Free-slot scan happens before rolling, while the material's own slot is still occupied, so it can
        // never be picked as its own destination.
        var hasFreeSlot = TryFindEmptySlot(state, out var resultPage, out var resultIndex);

        var resolved = CraftResolver.ResolveAdvancedElixir(material, hasFreeSlot, SystemRandomSource.Instance);

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Rejected)
            return new AdvancedElixirResult(AdvancedElixirOutcome.Rejected, null, 0, 0, null);

        var projectedMaterialContainer = resolved.RemainingMaterial is { } remainingMaterial
            ? state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, remainingMaterial)
            : state.Inventory.GetContainer((byte)page1).Remove((byte)index1);

        ImmutableArray<InventoryContainerSnapshot> containers;
        ItemStack? newItemStack = null;

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Success)
        {
            newItemStack = new ItemStack(resolved.ResultItemId!.Value, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                unchecked((int)DateTime.UtcNow.Ticks));

            if (resultPage == page1)
            {
                projectedMaterialContainer = projectedMaterialContainer.SetItem(resultIndex, newItemStack.Value);
                await characters.ReplaceContainerAsync(characterId, (byte)page1, ToTvps(projectedMaterialContainer),
                    cancellationToken);
                containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1,
                    projectedMaterialContainer));
            }
            else
            {
                var projectedResultContainer =
                    state.Inventory.GetContainer(resultPage).SetItem(resultIndex, newItemStack.Value);
                await characters.ReplaceTwoContainersAsync(characterId, (byte)page1,
                    ToTvps(projectedMaterialContainer), resultPage, ToTvps(projectedResultContainer),
                    cancellationToken);
                containers = ImmutableArray.Create(
                    new InventoryContainerSnapshot((byte)page1, projectedMaterialContainer),
                    new InventoryContainerSnapshot(resultPage, projectedResultContainer));
            }
        }
        else
        {
            await characters.ReplaceContainerAsync(characterId, (byte)page1, ToTvps(projectedMaterialContainer),
                cancellationToken);
            containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1,
                projectedMaterialContainer));
        }

        // Only the roll's success path actually mints a new item -- the 80% failure case still consumes the
        // material (see CraftResolver's own remarks) but creates nothing, so it gets no ItemCreate row.
        if (resolved.Outcome == CraftResolver.ElixirOutcome.Success)
        {
            var created = newItemStack!.Value;
            await eventLog.LogAsync(AdvancedElixirEventCode, EventLogCategory.ItemCreate, accountId, characterId,
                null, null, null, null, null, created.ItemId, created.Quantity, 1, null, cancellationToken);
        }

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (elixir) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        var outcome = resolved.Outcome == CraftResolver.ElixirOutcome.Success
            ? AdvancedElixirOutcome.Success
            : AdvancedElixirOutcome.Failed;

        return new AdvancedElixirResult(outcome, newItemStack, resultPage, resultIndex, resolved.RemainingMaterial);
    }

    private static bool IsValidInventorySlot(int page, int index)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, index);
    }

    private static bool TryFindEmptySlot(PlayerRuntimeState state, out byte page, out byte index)
    {
        foreach (var candidatePage in InventoryPagesInScanOrder)
        {
            ContainerMatrix.TryGetMaxSlot(candidatePage, out var maxSlot);
            for (var slot = 0; slot <= maxSlot; slot++)
                if (state.Inventory.GetSlot(candidatePage, (byte)slot) is null)
                {
                    page = candidatePage;
                    index = (byte)slot;
                    return true;
                }
        }

        page = 0;
        index = 0;
        return false;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
