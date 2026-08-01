using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class EnchantItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogQueue eventLogQueue,
    ILogger<EnchantItemService> logger)
    : IEnchantItemService
{
    private const short EnchantEventCode = 24;

    private const int ProtectItemStatSort = 15;

    private const int ProtectItem2StatSort = 104;

    private const int SweetPotatoStatSort = 146;

    public async ValueTask<EnchantItemResult> EnchantAsync(EnchantItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected: not in a town zone (zone {MapId})", characterId,
                zone.MapId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var now = DateTime.UtcNow;
        if (now - state.LastEnchantAttemptUtc < SimulationClock.LegacyTick)
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected: same-tick repeat request", characterId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        state.LastEnchantAttemptUtc = now;

        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1) ||
            page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page2, index2))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: invalid slot(s) ({Page1}:{Index1} / {Page2}:{Index2})",
                characterId, page1, index1, page2, index2);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var today = GameDate.Today();
        if (!RentedInventoryPageGate.IsPageAccessible(page1, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(page2, state.InventoryDate, today))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var materialStack = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition) ||
            !worldData.ItemsById.TryGetValue(material.ItemId, out var materialDefinition))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: target or material slot empty/unresolvable",
                characterId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var luck = state.Stats?.Luck ?? 0;

        var resolved = EnchantResolver.Resolve(targetDefinition, target, materialDefinition, luck,
            state.ProtectForDestroy, state.ImproveItemValue, SystemRandomSource.Instance,
            state.ProtectForDestroy2);

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.Rejected)
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected by resolver (target {TargetItemId}, material {MaterialItemId})",
                characterId, target.ItemId, material.ItemId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        if (resolved.IsWing && state.ContributionPoints < resolved.Cost)
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected: insufficient CP for wing enchant (have {ContributionPoints}, need {Cost})",
                characterId, state.ContributionPoints, resolved.Cost);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = remainingMaterialQuantity > 0
            ? material with { Quantity = remainingMaterialQuantity }
            : (ItemStack?)null;

        ItemStack? newTargetStack = resolved.Outcome == EnchantResolver.EnchantOutcome.Destroyed
            ? null
            : target with { Enchant = (byte)resolved.NewEnchant };

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

        var moneyDelta = resolved.IsWing ? 0 : -resolved.Cost;

        try
        {
            if (page1 == page2)
                await characters.AdjustMoneyAndReplaceContainerAsync(characterId, moneyDelta, 0, (byte)page1,
                    ToTvps(projectedTargetContainer), cancellationToken);
            else
                await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, moneyDelta, 0,
                    (byte)page1, ToTvps(projectedTargetContainer), (byte)page2, ToTvps(projectedMaterialContainer),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} enchant AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        int? newProtectForDestroy = resolved.ConsumesProtectCharge ? state.ProtectForDestroy - 1 : null;
        int? newProtectForDestroy2 = resolved.ConsumesProtectCharge2 ? state.ProtectForDestroy2 - 1 : null;
        int? newImproveItemValue = resolved.ConsumesImproveCharge ? state.ImproveItemValue - 1 : null;

        if (resolved.IsWing)
        {
            var newContributionPoints = state.ContributionPoints - resolved.Cost;
            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(characterId, newContributionPoints,
                        ProtectForDestroy: newProtectForDestroy, ProtectForDestroy2: newProtectForDestroy2,
                        ImproveItemValue: newImproveItemValue),
                    cancellationToken))
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped CP/charge mirror for character {CharacterId} after wing enchant -- SQL write-behind will retry on next dirty flush",
                    zone.MapId, characterId);
        }
        else
        {
            zone.CreditNpcServiceTribeTax(state.Tribe, resolved.Cost);

            if (newProtectForDestroy is not null || newProtectForDestroy2 is not null ||
                newImproveItemValue is not null)
                if (!await zone.PostTribeProgressCommandAndWaitAsync(
                        new TribeProgressZoneCommand(characterId, ProtectForDestroy: newProtectForDestroy,
                            ProtectForDestroy2: newProtectForDestroy2, ImproveItemValue: newImproveItemValue),
                        cancellationToken))
                    logger.LogError(
                        "Zone {MapId} tribe-progress inbox full: dropped protect/sweet-potato charge mirror for character {CharacterId}",
                        zone.MapId, characterId);
        }

        // S146SWEET_POTATO part des le calcul de probabilite, donc avant les sorts de protection et avant
        // IMPROVE_ITEM_RECV, seulement au joueur: Server/ts25zone/S04_MyWork02.cpp:3160.
        if (newImproveItemValue is { } remainingSweetPotatoCharges)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = SweetPotatoStatSort, Value = remainingSweetPotatoCharges, Value2 = 0 });

        // S104PROTECT_ITEM2 part avant IMPROVE_ITEM_RECV et seulement au joueur: Server/ts25zone/S04_MyWork02.cpp:2945.
        if (newProtectForDestroy2 is { } remainingProtect2Charges)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = ProtectItem2StatSort, Value = remainingProtect2Charges, Value2 = 0 });

        // S015PROTECT_ITEM part avant IMPROVE_ITEM_RECV et seulement au joueur: Server/ts25zone/S04_MyWork02.cpp:3368.
        if (newProtectForDestroy is { } remainingProtectCharges)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = ProtectItemStatSort, Value = remainingProtectCharges, Value2 = 0 });

        var resultCode = MapResultCode(resolved.Outcome, resolved.IsWing);

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.Success)
        {
            var reachedCap = resolved.IsWing
                ? resolved.NewEnchant == CenterRelayNoticeLog.EnchantCapValue
                : resolved.NewEnchant == CenterRelayNoticeLog.EnchantCapValue ||
                  resolved.NewEnchant > CenterRelayNoticeLog.EnchantCapValue + 1;

            if (reachedCap)
                CenterRelayNoticeLog.LogEnchantCap(logger, state.Tribe, state.Name, resolved.NewEnchant,
                    resolved.IsWing);
        }

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(EnchantEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, resolved.IsWing ? null : -(long)resolved.Cost, null, target.ItemId,
                target.Quantity, (byte)resultCode,
                resolved.IsWing
                    ? $"Serial={target.Serial};From={target.Enchant};To={resolved.NewEnchant};Material={material.ItemId};CpCost={resolved.Cost}"
                    : $"Serial={target.Serial};From={target.Enchant};To={resolved.NewEnchant};Material={material.ItemId}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped enchant-attempt audit row for character {CharacterId}",
                characterId);

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projectedTargetContainer),
                new InventoryContainerSnapshot((byte)page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped enchant mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} enchant applied: target {TargetItemId} outcome {Outcome} -> enchant {NewEnchant}, cost {Cost}",
            characterId, target.ItemId, resolved.Outcome, resolved.NewEnchant, resolved.Cost);

        return new EnchantItemResult(EnchantItemOutcome.Applied, resultCode, resolved.Cost, resolved.NewEnchant);
    }

    private static int MapResultCode(EnchantResolver.EnchantOutcome outcome, bool isWing)
    {
        return outcome switch
        {
            EnchantResolver.EnchantOutcome.Unsealed => 0,
            EnchantResolver.EnchantOutcome.Success => 0,
            EnchantResolver.EnchantOutcome.Failed => 1,
            EnchantResolver.EnchantOutcome.Destroyed => 2,
            EnchantResolver.EnchantOutcome.ResetToForty => 3,
            EnchantResolver.EnchantOutcome.Protected => 4,
            EnchantResolver.EnchantOutcome.NoChange => isWing ? 9 : 8,
            _ => 1
        };
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
