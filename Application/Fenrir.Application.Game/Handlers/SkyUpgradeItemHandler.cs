using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Enchant;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op93, CZ_SKY_UP_ITEM_SEND -- Warlord-item-only upgrade via <see cref="SkyUpgradeResolver" />. Money is
///     always deducted and the material always consumed regardless of outcome (matches the legacy's own
///     unconditional <c>wAvatar.aMoney -= tCost</c>/<c>DecreaseMaterial</c> placement before the roll).
/// </summary>
public sealed class SkyUpgradeItemHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<SkyUpgradeItemHandler> logger)
    : IAsyncPacketHandler<SkyUpgradeItemRequest>
{
    public async ValueTask HandleAsync(SkyUpgradeItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(SkyUpgradeItemRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1) ||
            page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page2, index2))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = SkyUpgradeResolver.Resolve(targetDefinition.Item, target.Enchant, material.ItemId,
            SystemRandomSource.Instance);

        if (resolved.Outcome == SkyUpgradeResolver.Outcome.Rejected)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = remainingMaterialQuantity > 0
            ? material with { Quantity = remainingMaterialQuantity }
            : (ItemStack?)null;

        var newTargetStack = resolved.Succeeded
            ? target with { ItemId = resolved.NewItemId, Enchant = resolved.NewEnchant }
            : target;

        ImmutableDictionary<byte, ItemStack> projectedTargetContainer;
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer;

        if (page1 == page2)
        {
            var combined = ApplySlotChange(state.Inventory.GetContainer((byte)page1), (byte)index1, newTargetStack);
            combined = ApplySlotChange(combined, (byte)index2, newMaterialStack);
            projectedTargetContainer = combined;
            projectedMaterialContainer = combined;
        }
        else
        {
            projectedTargetContainer =
                ApplySlotChange(state.Inventory.GetContainer((byte)page1), (byte)index1, newTargetStack);
            projectedMaterialContainer =
                ApplySlotChange(state.Inventory.GetContainer((byte)page2), (byte)index2, newMaterialStack);
        }

        try
        {
            if (page1 == page2)
                await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -SkyUpgradeResolver.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), cancellationToken);
            else
                await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, -SkyUpgradeResolver.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), (byte)page2, ToTvps(projectedMaterialContainer),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} sky-upgrade AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var packedValue = ItemValueCodec.Encode(newTargetStack.Enchant, newTargetStack.Combine,
            newTargetStack.Refine, newTargetStack.Socket);

        session.Send(new SkyUpgradeItemResponse
        {
            Result = resolved.Succeeded ? 0 : 1,
            Cost = SkyUpgradeResolver.Cost,
            Value = [newTargetStack.ItemId, index1 % 8, index1 / 8, target.Quantity, packedValue, target.Serial]
        });

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped sky-upgrade mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    private static ImmutableDictionary<byte, ItemStack> ApplySlotChange(
        ImmutableDictionary<byte, ItemStack> current, byte slot, ItemStack? value)
    {
        return value is { } v ? current.SetItem(slot, v) : current.Remove(slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
