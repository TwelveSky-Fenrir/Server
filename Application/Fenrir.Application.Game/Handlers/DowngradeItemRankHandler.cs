using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Forge;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op28, CZ_LOW_ITEM_SEND -- downgrades a Rare/Elite item to the previous tier via
///     <see cref="RankChangeResolver" />. Unlike <see cref="UpgradeItemRankHandler" />, a successful downgrade
///     only swaps the item id -- Enchant/Combine/Refine/Socket are all left exactly as they were.
/// </summary>
public sealed class DowngradeItemRankHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<DowngradeItemRankHandler> logger)
    : IAsyncPacketHandler<DowngradeItemRankRequest>
{
    public async ValueTask HandleAsync(DowngradeItemRankRequest packet, IPacketSession session,
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

    private async ValueTask ResolveAndApplyAsync(DowngradeItemRankRequest packet, IPacketSession session,
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

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition) ||
            !worldData.ItemsById.TryGetValue(material.ItemId, out var materialDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var luck = state.Stats?.Luck ?? 0;

        var resolved = RankChangeResolver.ResolveDowngrade(targetDefinition, target, materialDefinition.Item, luck, 0,
            worldData.ItemsById.Values, SystemRandomSource.Instance);

        switch (resolved.Outcome)
        {
            case RankChangeResolver.RankChangeOutcome.Rejected:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case RankChangeResolver.RankChangeOutcome.NoCandidate:
                session.Send(new DowngradeItemRankResponse
                {
                    Result = 2, Cost = resolved.Cost, Value = [0, 0, 0, 0, 0, 0]
                });
                return;
        }

        var succeeded = resolved.Outcome == RankChangeResolver.RankChangeOutcome.Success;

        var newTargetStack = succeeded ? target with { ItemId = resolved.ResultItemId } : target;

        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = remainingMaterialQuantity > 0
            ? material with { Quantity = remainingMaterialQuantity }
            : (ItemStack?)null;

        ImmutableDictionary<byte, ItemStack> projectedTargetContainer;
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer;

        if (page1 == page2)
        {
            var combined = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newTargetStack);
            combined = ApplySlotChange(combined, (byte)index2, newMaterialStack);
            projectedTargetContainer = combined;
            projectedMaterialContainer = combined;
        }
        else
        {
            projectedTargetContainer = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newTargetStack);
            projectedMaterialContainer = ApplySlotChange(state.Inventory.GetContainer((byte)page2), (byte)index2,
                newMaterialStack);
        }

        try
        {
            if (page1 == page2)
                await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -resolved.Cost, 0, (byte)page1,
                    ToTvps(projectedTargetContainer), cancellationToken);
            else
                await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, -resolved.Cost, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), (byte)page2, ToTvps(projectedMaterialContainer),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} downgrade-item-rank AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var value = succeeded
            ? new[]
            {
                resolved.ResultItemId, 0, 0, target.Quantity,
                ItemValueCodec.Encode(target.Enchant, target.Combine, target.Refine, target.Socket),
                target.Serial
            }
            : [0, 0, 0, 0, 0, 0];

        session.Send(new DowngradeItemRankResponse { Result = succeeded ? 0 : 1, Cost = resolved.Cost, Value = value });

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped downgrade-item-rank mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    private static bool IsValidInventorySlot(int page, int index)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, index);
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
