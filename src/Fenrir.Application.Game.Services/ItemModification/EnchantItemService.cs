using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain;
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
    GameServerOptions gameOptions,
    ILogger<EnchantItemService> logger)
    : IEnchantItemService
{
    private const short EnchantEventCode = 24;

    private const int ProtectItemStatSort = 15;

    private const int ProtectItem2StatSort = 104;

    private const int WingProtectStatSort = 99;

    private const int SweetPotatoStatSort = 146;

    public async ValueTask<EnchantItemResult> EnchantAsync(EnchantItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected: not in a town zone (zone {MapId})", characterId,
                zone.MapId);
            return AbortAndDisconnect(state, characterId, "not in a town zone");
        }

        var now = DateTime.UtcNow;
        if (now - state.LastEnchantAttemptUtc < SimulationClock.LegacyTick)
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected: same-tick repeat request", characterId);
            return AbortAndDisconnect(state, characterId, "same-tick repeat request");
        }

        if (packet.Page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.Page1, packet.Index1) ||
            packet.Page2 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)packet.Page2, packet.Index2))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: invalid slot(s) ({Page1}:{Index1} / {Page2}:{Index2})",
                characterId, packet.Page1, packet.Index1, packet.Page2, packet.Index2);
            return AbortAndDisconnect(state, characterId, "invalid inventory slot");
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
            return AbortAndDisconnect(state, characterId, "expired inventory page");
        }

        var targetStack = state.Inventory.GetSlot(slots.Page1, slots.Index1);
        var materialStack = state.Inventory.GetSlot(slots.Page2, slots.Index2);

        if (targetStack is not { } target || materialStack is not { } material ||
            target.Quantity <= 0 || material.Quantity <= 0 ||
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition) ||
            !worldData.ItemsById.TryGetValue(material.ItemId, out var materialDefinition))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: target or material slot empty/unresolvable",
                characterId);
            return AbortAndDisconnect(state, characterId, "missing or invalid target/material item");
        }

        if (NpcShopPolicy.IsRentItem(target.ItemId) || NpcShopPolicy.IsRentItem(material.ItemId))
        {
            logger.LogDebug(
                "Character {CharacterId} enchant rejected: rent-listed item in target or material slot",
                characterId);
            return AbortAndDisconnect(state, characterId, "rent-listed item");
        }

        var subPath = EnchantDispatchClassifier.Classify(target.ItemId, material.ItemId);

        return subPath switch
        {
            EnchantSubPath.CostumeSwap =>
                await EnchantCostumeSwapAsync(zone, state, characterId, now, slots, target, material,
                    cancellationToken),
            EnchantSubPath.CostumeEnchant =>
                await EnchantCostumeEnchantAsync(zone, state, characterId, now, slots, target, material,
                    cancellationToken),
            EnchantSubPath.StellarCoreUpgrade =>
                await EnchantStellarCoreAsync(zone, state, characterId, now, slots, target, targetDefinition, material,
                    materialDefinition, cancellationToken),
            EnchantSubPath.Standard =>
                await EnchantStandardAsync(zone, state, characterId, now, slots, target, targetDefinition, material,
                    materialDefinition, cancellationToken),
            _ => AbortAndDisconnect(state, characterId, "unrecognized enchant path")
        };
    }

    private async ValueTask<EnchantItemResult> EnchantStandardAsync(Zone zone, PlayerRuntimeState state,
        int characterId, DateTime attemptUtc, EnchantSlots slots, ItemStack target, ItemDefinition targetDefinition,
        ItemStack material,
        ItemDefinition materialDefinition, CancellationToken cancellationToken)
    {
        var luck = state.Stats?.Luck ?? 0;

        var premiumActive = state.PremiumExpireUtc >= DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (!GameRulesetRules.TryParse(gameOptions.Ruleset, out var ruleset))
            throw new InvalidOperationException("Game:Ruleset was not validated before enchant resolution.");

        var resolved = EnchantResolver.Resolve(targetDefinition, target, materialDefinition, luck,
            state.ProtectForDestroy, state.ImproveItemValue, SystemRandomSource.Instance,
            ruleset, state.ProtectForDestroy2, state.ProtectForWing, premiumActive);

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.Disconnect)
            return AbortAndDisconnect(state, characterId,
                $"ruleset {ruleset} enchant cap reached ({target.Enchant})");

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.Rejected)
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected by resolver (target {TargetItemId}, material {MaterialItemId})",
                characterId, target.ItemId, material.ItemId);
            return AbortAndDisconnect(state, characterId, "invalid enchant eligibility");
        }

        if (resolved.IsWing && state.ContributionPoints < resolved.Cost)
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected: insufficient CP for wing enchant (have {ContributionPoints}, need {Cost})",
                characterId, state.ContributionPoints, resolved.Cost);
            return AbortAndDisconnect(state, characterId, "insufficient contribution points");
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

        try
        {
            await PersistContainersAsync(characterId, moneyDelta, slots, projectedTargetContainer,
                projectedMaterialContainer, cancellationToken);
        }
        catch (Exception ex)
        {
            return AbortAfterUncertainPersistence(state, characterId, "enchant", ex);
        }

        int? newProtectForDestroy = resolved.ConsumesProtectCharge ? state.ProtectForDestroy - 1 : null;
        int? newProtectForDestroy2 = resolved.ConsumesProtectCharge2 ? state.ProtectForDestroy2 - 1 : null;
        int? newProtectForWing = resolved.ConsumesWingProtectCharge ? state.ProtectForWing - 1 : null;
        int? newImproveItemValue = resolved.ConsumesImproveCharge ? state.ImproveItemValue - 1 : null;

        var inventoryResult = await PostInventoryMirrorAsync(zone, characterId, slots, projectedTargetContainer,
            projectedMaterialContainer, cancellationToken);
        if (inventoryResult.Kind != ZoneCommandResultKind.Applied)
            return AbortAfterDurableMutation(state, characterId, "enchant inventory", inventoryResult);

        if (resolved.IsWing)
        {
            var newContributionPoints = state.ContributionPoints - resolved.Cost;
            var tribeResult = await zone.PostTribeProgressCommandAndWaitForResultAsync(
                new TribeProgressZoneCommand(characterId, newContributionPoints,
                    ProtectForDestroy: newProtectForDestroy, ProtectForDestroy2: newProtectForDestroy2,
                    ProtectForWing: newProtectForWing, ImproveItemValue: newImproveItemValue),
                cancellationToken);
            if (tribeResult.Kind != ZoneCommandResultKind.Applied)
                return AbortAfterDurableMutation(state, characterId, "wing-enchant progress", tribeResult);
        }
        else
        {
            if (newProtectForDestroy is not null || newProtectForDestroy2 is not null ||
                newImproveItemValue is not null)
            {
                var tribeResult = await zone.PostTribeProgressCommandAndWaitForResultAsync(
                    new TribeProgressZoneCommand(characterId, ProtectForDestroy: newProtectForDestroy,
                        ProtectForDestroy2: newProtectForDestroy2, ImproveItemValue: newImproveItemValue),
                    cancellationToken);
                if (tribeResult.Kind != ZoneCommandResultKind.Applied)
                    return AbortAfterDurableMutation(state, characterId, "enchant charge", tribeResult);
            }
        }

        state.LastEnchantAttemptUtc = attemptUtc;

        if (!resolved.IsWing)
            zone.CreditNpcServiceTribeTax(state.Tribe, resolved.Cost);

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

        var resultCode = MapResultCode(in resolved);

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

        logger.LogInformation(
            "Character {CharacterId} enchant applied: target {TargetItemId} outcome {Outcome} -> enchant {NewEnchant}, cost {Cost}",
            characterId, target.ItemId, resolved.Outcome, resolved.NewEnchant, resolved.Cost);

        return new EnchantItemResult(EnchantItemOutcome.Applied, resultCode, resolved.Cost, resolved.NewEnchant);
    }

    private static int MapResultCode(in EnchantResolver.EnchantResult resolved)
    {
        return resolved.Outcome switch
        {
            EnchantResolver.EnchantOutcome.Unsealed => 0,
            EnchantResolver.EnchantOutcome.Success => 0,
            EnchantResolver.EnchantOutcome.Failed => 1,
            EnchantResolver.EnchantOutcome.Destroyed => 2,
            EnchantResolver.EnchantOutcome.ResetToForty => 3,
            EnchantResolver.EnchantOutcome.Protected => resolved.ConsumesProtectCharge2 ? 4 : 1,
            EnchantResolver.EnchantOutcome.NoChange => resolved.IsWing ? 9 : 8,
            _ => 1
        };
    }

    private EnchantItemResult AbortAndDisconnect(PlayerRuntimeState state, int characterId, string reason)
    {
        logger.LogInformation("Character {CharacterId} enchant disconnected: {Reason}", characterId, reason);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new EnchantItemResult(EnchantItemOutcome.Disconnected, 0, 0, 0);
    }

    private EnchantItemResult AbortAfterUncertainPersistence(PlayerRuntimeState state, int characterId,
        string mutation, Exception exception)
    {
        logger.LogError(exception,
            "Character {CharacterId} {Mutation} persistence failed after submission; durability is uncertain, disconnecting without success response",
            characterId, mutation);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new EnchantItemResult(EnchantItemOutcome.Disconnected, 0, 0, 0);
    }

    private EnchantItemResult AbortAfterDurableMutation(PlayerRuntimeState state, int characterId,
        string mutation, ZoneCommandResult result)
    {
        logger.LogError(
            "Character {CharacterId} enchant persisted but {Mutation} actor mutation was not acknowledged as applied ({Kind}: {Cause}); disconnecting without success response",
            characterId, mutation, result.Kind, result.Cause);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new EnchantItemResult(EnchantItemOutcome.Disconnected, 0, 0, 0);
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

    private async ValueTask PersistContainersAsync(int characterId, long moneyDelta, EnchantSlots slots,
        ImmutableDictionary<byte, ItemStack> projectedTargetContainer,
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer, CancellationToken cancellationToken)
    {
        if (slots.Page1 == slots.Page2)
            await characters.AdjustMoneyAndReplaceContainerAsync(characterId, moneyDelta, 0, slots.Page1,
                ToTvps(projectedTargetContainer), cancellationToken);
        else
            await characters.AdjustMoneyAndReplaceTwoContainersAsync(characterId, moneyDelta, 0, slots.Page1,
                ToTvps(projectedTargetContainer), slots.Page2, ToTvps(projectedMaterialContainer),
                cancellationToken);
    }

    private async ValueTask<ZoneCommandResult> PostInventoryMirrorAsync(Zone zone, int characterId, EnchantSlots slots,
        ImmutableDictionary<byte, ItemStack> projectedTargetContainer,
        ImmutableDictionary<byte, ItemStack> projectedMaterialContainer, CancellationToken cancellationToken)
    {
        var containers = slots.Page1 == slots.Page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot(slots.Page1, projectedTargetContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(slots.Page1, projectedTargetContainer),
                new InventoryContainerSnapshot(slots.Page2, projectedMaterialContainer));

        return await zone.PostInventoryCommandAndWaitForResultAsync(
            new InventoryZoneCommand(characterId, containers, null), cancellationToken);
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

    private readonly record struct EnchantSlots(byte Page1, byte Index1, byte Page2, byte Index2);
}
