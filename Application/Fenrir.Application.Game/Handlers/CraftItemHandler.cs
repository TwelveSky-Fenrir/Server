using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Crafting;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op29, CZ_MAKE_ITEM_SEND -- implements the two recipes <see cref="CraftRecipeCatalog" />/
///     <see cref="CraftResolver" /> cover (Jade upgrade, advanced elixir); every other MK_* sort aborts,
///     matching the legacy's own <c>default: Quit()</c>.
/// </summary>
public sealed class CraftItemHandler(
    ICharacterRepository characters,
    ILogger<CraftItemHandler> logger)
    : IAsyncPacketHandler<CraftItemRequest>
{
    private static readonly byte[] InventoryPagesInScanOrder =
        [ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1];

    public async ValueTask HandleAsync(CraftItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes the read/SQL/mirror sequence per character to close an item-duplication window.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            switch (packet.Sort)
            {
                case CraftRecipeCatalog.JadeUpgradeSort:
                    await HandleJadeUpgradeAsync(packet, session, zoneSession, zone, state, characterId,
                        cancellationToken);
                    return;
                case CraftRecipeCatalog.AdvancedElixirSort:
                    await HandleAdvancedElixirAsync(packet, session, zoneSession, zone, state, characterId,
                        cancellationToken);
                    return;
                default:
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask HandleJadeUpgradeAsync(CraftItemRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (!IsValidInventorySlot(page1, index1) || !IsValidInventorySlot(page2, index2))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var material1 = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var material2 = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (material1 is not { } m1 || material2 is not { } m2)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = CraftResolver.ResolveJadeUpgrade(m1, m2);
        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

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

        // X/Y sub-grid position has no Fenrir-side backing (ContainerMatrix is flat-slot) -- left 0, cosmetic only.
        session.Send(new CraftItemResponse
        {
            Result = 0, Value = [result.ItemId, 0, 0, 0, 0, result.Serial]
        });

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
    }

    private async ValueTask HandleAdvancedElixirAsync(CraftItemRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (!IsValidInventorySlot(page1, index1))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var materialStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (materialStack is not { } material)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Free-slot scan happens before rolling, while the material's own slot is still occupied, so it can
        // never be picked as its own destination.
        var hasFreeSlot = TryFindEmptySlot(state, out var resultPage, out var resultIndex);

        var resolved = CraftResolver.ResolveAdvancedElixir(material, hasFreeSlot, SystemRandomSource.Instance);

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Rejected)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var projectedMaterialContainer = resolved.RemainingMaterial is { } remainingMaterial
            ? state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, remainingMaterial)
            : state.Inventory.GetContainer((byte)page1).Remove((byte)index1);

        ImmutableArray<InventoryContainerSnapshot> containers;

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Success)
        {
            var newItemStack = new ItemStack(resolved.ResultItemId!.Value, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                unchecked((int)DateTime.UtcNow.Ticks));

            if (resultPage == page1)
            {
                projectedMaterialContainer = projectedMaterialContainer.SetItem(resultIndex, newItemStack);
                await characters.ReplaceContainerAsync(characterId, (byte)page1, ToTvps(projectedMaterialContainer),
                    cancellationToken);
                containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1,
                    projectedMaterialContainer));
            }
            else
            {
                var projectedResultContainer =
                    state.Inventory.GetContainer(resultPage).SetItem(resultIndex, newItemStack);
                await characters.ReplaceTwoContainersAsync(characterId, (byte)page1,
                    ToTvps(projectedMaterialContainer), resultPage, ToTvps(projectedResultContainer),
                    cancellationToken);
                containers = ImmutableArray.Create(
                    new InventoryContainerSnapshot((byte)page1, projectedMaterialContainer),
                    new InventoryContainerSnapshot(resultPage, projectedResultContainer));
            }

            // B_MAKE_ITEM_RECV describes the CONSUMED material slot, not the new item -- the new item rides the
            // separate ZC_ADD_USER_INVENTORY_ITEM_RECV, sent first so the client learns of the new item before
            // the craft result referencing the consumed slot.
            session.Send(new AddInventoryItemResponse
            {
                Result = 0,
                ItemIndex = newItemStack.ItemId,
                Page = resultPage,
                Index = resultIndex,
                Xy = 0,
                Quantity = newItemStack.Quantity,
                Value = 0,
                Serial = newItemStack.Serial,
                Socket = [0, 0, 0],
                Expire = 0
            });
            session.Send(new CraftItemResponse
            {
                Result = MaterialResultCode(resolved.RemainingMaterial),
                Value = MaterialValue(resolved.RemainingMaterial)
            });
        }
        else
        {
            await characters.ReplaceContainerAsync(characterId, (byte)page1, ToTvps(projectedMaterialContainer),
                cancellationToken);
            session.Send(new CraftItemResponse
            {
                Result = MaterialResultCode(resolved.RemainingMaterial),
                Value = MaterialValue(resolved.RemainingMaterial)
            });
            containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1,
                projectedMaterialContainer));
        }

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (elixir) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    /// <summary>1001 if the material slot emptied out, 10001 if some quantity remains.</summary>
    private static int MaterialResultCode(ItemStack? remainingMaterial)
    {
        return remainingMaterial is null ? 1001 : 10001;
    }

    private static int[] MaterialValue(ItemStack? remainingMaterial)
    {
        return remainingMaterial is { } m ? [m.ItemId, 0, 0, m.Quantity, 0, m.Serial] : [0, 0, 0, 0, 0, 0];
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
