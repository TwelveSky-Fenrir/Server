using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
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

public sealed partial class EnchantItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogQueue eventLogQueue,
    ILogger<EnchantItemService> logger)
    : IEnchantItemService
{
    private const short EnchantEventCode = 24;

    private const int ProtectItemStatSort = 15;

    private const int ProtectItem2StatSort = 104;

    private const int WingProtectStatSort = 99;

    private const int SweetPotatoStatSort = 146;

    private readonly record struct EnchantSlots(byte Page1, byte Index1, byte Page2, byte Index2);

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

        if (packet.Page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.Page1, packet.Index1) ||
            packet.Page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.Page2, packet.Index2))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: invalid slot(s) ({Page1}:{Index1} / {Page2}:{Index2})",
                characterId, packet.Page1, packet.Index1, packet.Page2, packet.Index2);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var slots = new EnchantSlots((byte)packet.Page1, (byte)packet.Index1, (byte)packet.Page2,
            (byte)packet.Index2);

        var today = GameDate.Today();
        if (!RentedInventoryPageGate.IsPageAccessible(slots.Page1, state.InventoryDate, today) ||
            !RentedInventoryPageGate.IsPageAccessible(slots.Page2, state.InventoryDate, today))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var targetStack = state.Inventory.GetSlot(slots.Page1, slots.Index1);
        var materialStack = state.Inventory.GetSlot(slots.Page2, slots.Index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition) ||
            !worldData.ItemsById.TryGetValue(material.ItemId, out var materialDefinition))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: target or material slot empty/unresolvable",
                characterId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        if (NpcShopPolicy.IsRentItem(target.ItemId) || NpcShopPolicy.IsRentItem(material.ItemId))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: rent-listed item in target or material slot",
                characterId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        var subPath = EnchantDispatchClassifier.Classify(target.ItemId, material.ItemId);

        return subPath switch
        {
            EnchantSubPath.CostumeSwap =>
                await EnchantCostumeSwapAsync(zone, state, characterId, slots, target, material, cancellationToken),
            EnchantSubPath.CostumeEnchant =>
                await EnchantCostumeEnchantAsync(zone, state, characterId, slots, target, material,
                    cancellationToken),
            EnchantSubPath.StellarCoreUpgrade =>
                await EnchantStellarCoreAsync(zone, state, characterId, slots, target, targetDefinition, material,
                    materialDefinition, cancellationToken),
            EnchantSubPath.Standard =>
                await EnchantStandardAsync(zone, state, characterId, slots, target, targetDefinition, material,
                    materialDefinition, cancellationToken),
            _ => new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0)
        };
    }

    private async ValueTask<EnchantItemResult> EnchantStandardAsync(Zone zone, PlayerRuntimeState state,
        int characterId, EnchantSlots slots, ItemStack target, ItemDefinition targetDefinition, ItemStack material,
        ItemDefinition materialDefinition, CancellationToken cancellationToken)
    {
        var luck = state.Stats?.Luck ?? 0;

        var resolved = EnchantResolver.Resolve(targetDefinition, target, materialDefinition, luck,
            state.ProtectForDestroy, state.ImproveItemValue, SystemRandomSource.Instance,
            state.ProtectForDestroy2, state.ProtectForWing);

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

        var (projectedTargetContainer, projectedMaterialContainer) =
            ProjectContainers(state, slots, newTargetStack, newMaterialStack);

        var moneyDelta = resolved.IsWing ? 0 : -resolved.Cost;

        if (!await TryPersistContainersAsync(characterId, moneyDelta, slots, projectedTargetContainer,
                projectedMaterialContainer, cancellationToken))
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);

        int? newProtectForDestroy = resolved.ConsumesProtectCharge ? state.ProtectForDestroy - 1 : null;
        int? newProtectForDestroy2 = resolved.ConsumesProtectCharge2 ? state.ProtectForDestroy2 - 1 : null;
        int? newProtectForWing = resolved.ConsumesWingProtectCharge ? state.ProtectForWing - 1 : null;
        int? newImproveItemValue = resolved.ConsumesImproveCharge ? state.ImproveItemValue - 1 : null;

        if (resolved.IsWing)
        {
            var newContributionPoints = state.ContributionPoints - resolved.Cost;
            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(characterId, newContributionPoints,
                        ProtectForDestroy: newProtectForDestroy, ProtectForDestroy2: newProtectForDestroy2,
                        ProtectForWing: newProtectForWing, ImproveItemValue: newImproveItemValue),
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

        if (newImproveItemValue is { } remainingSweetPotatoCharges)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = SweetPotatoStatSort, Value = remainingSweetPotatoCharges, Value2 = 0 });

        if (newProtectForDestroy2 is { } remainingProtect2Charges)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = ProtectItem2StatSort, Value = remainingProtect2Charges, Value2 = 0 });

        if (newProtectForDestroy is { } remainingProtectCharges)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = ProtectItemStatSort, Value = remainingProtectCharges, Value2 = 0 });

        if (newProtectForWing is { } remainingWingProtectCharges)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = WingProtectStatSort, Value = remainingWingProtectCharges, Value2 = 0 });

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
                    ? resolved.ConsumesWingProtectCharge
                        ? $"Tag=PT_WING;Serial={target.Serial};From={target.Enchant};To={resolved.NewEnchant};Material={material.ItemId};CpCost={resolved.Cost}"
                        : $"Serial={target.Serial};From={target.Enchant};To={resolved.NewEnchant};Material={material.ItemId};CpCost={resolved.Cost}"
                    : $"Serial={target.Serial};From={target.Enchant};To={resolved.NewEnchant};Material={material.ItemId}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped enchant-attempt audit row for character {CharacterId}",
                characterId);

        await PostInventoryMirrorAsync(zone, characterId, slots, projectedTargetContainer,
            projectedMaterialContainer, cancellationToken);

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

    private EnchantItemResult AbortAndRejectSilently(PlayerRuntimeState state, int characterId, string reason)
    {
        logger.LogInformation("Character {CharacterId} enchant disconnected: {Reason}", characterId, reason);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
    }

    private static (ImmutableDictionary<byte, ItemStack> Target, ImmutableDictionary<byte, ItemStack> Material)
        ProjectContainers(PlayerRuntimeState state, EnchantSlots slots, ItemStack? newTargetStack,
            ItemStack? newMaterialStack)
    {
        if (slots.Page1 == slots.Page2)
        {
            var combined = ApplySlotChange(state.Inventory.GetContainer(slots.Page1), slots.Index1, newTargetStack);
            combined = ApplySlotChange(combined, slots.Index2, newMaterialStack);
            return (combined, combined);
        }

        var target = ApplySlotChange(state.Inventory.GetContainer(slots.Page1), slots.Index1, newTargetStack);
        var material = ApplySlotChange(state.Inventory.GetContainer(slots.Page2), slots.Index2, newMaterialStack);
        return (target, material);
    }

    private async ValueTask<bool> TryPersistContainersAsync(int characterId, long moneyDelta, EnchantSlots slots,
        ImmutableDictionary<byte, ItemStack> projectedTargetContainer,
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer, CancellationToken cancellationToken)
    {
        try
        {
            if (slots.Page1 == slots.Page2)
                await characters.AdjustMoneyAndReplaceContainerAsync(characterId, moneyDelta, 0, slots.Page1,
                    ToTvps(projectedTargetContainer), cancellationToken);
            else
                await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, moneyDelta, 0, slots.Page1,
                    ToTvps(projectedTargetContainer), slots.Page2, ToTvps(projectedMaterialContainer),
                    cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} enchant AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            return false;
        }
    }

    private async ValueTask PostInventoryMirrorAsync(Zone zone, int characterId, EnchantSlots slots,
        ImmutableDictionary<byte, ItemStack> projectedTargetContainer,
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer, CancellationToken cancellationToken)
    {
        var containers = slots.Page1 == slots.Page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot(slots.Page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(slots.Page1, projectedTargetContainer),
                new InventoryContainerSnapshot(slots.Page2, projectedMaterialContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped enchant mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
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
