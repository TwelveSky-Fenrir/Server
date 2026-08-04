using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed partial class EnchantItemService
{
    private const int CostumeSwapResultCode = 999;

    private const byte CostumeSwapAuditOutcome = 0;

    private const int CostumeEnchantSuccessResultCode = 0;

    private const int CostumeEnchantFailureResultCode = 8;

    private async ValueTask<EnchantItemResult> EnchantCostumeSwapAsync(Zone zone, PlayerRuntimeState state,
        int characterId, DateTime attemptUtc, EnchantSlots slots, ItemStack target, ItemStack material,
        CancellationToken cancellationToken)
    {
        var resolved = CostumeImproveResolver.ResolveSwap(target.Enchant, material.Enchant);

        if (resolved.Outcome == CostumeImproveResolver.CostumeSwapOutcome.Rejected)
            return AbortAndDisconnect(state, characterId,
                "costume swap ordering rule violated (IsCostumeForSwap)");

        var newTargetStack = target with
        {
            Enchant = material.Enchant,
            Combine = material.Combine,
            Refine = material.Refine,
            Socket = material.Socket
        };
        var newMaterialStack = material with
        {
            Enchant = target.Enchant,
            Combine = target.Combine,
            Refine = target.Refine,
            Socket = target.Socket
        };

        var (projectedTarget, projectedMaterial) = ProjectContainers(state, slots, newTargetStack, newMaterialStack);

        try
        {
            await PersistContainersAsync(characterId, -resolved.MoneyCost, slots, projectedTarget, projectedMaterial,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return AbortAfterUncertainPersistence(state, characterId, "costume swap", ex);
        }

        var inventoryResult = await PostInventoryMirrorAsync(zone, characterId, slots, projectedTarget,
            projectedMaterial, cancellationToken);
        if (inventoryResult.Kind != ZoneCommandResultKind.Applied)
            return AbortAfterDurableMutation(state, characterId, "costume swap inventory", inventoryResult);

        state.LastEnchantAttemptUtc = attemptUtc;

        zone.CreditNpcServiceTribeTax(state.Tribe, resolved.MoneyCost);

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(EnchantEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, -(long)resolved.MoneyCost, null, target.ItemId, target.Quantity,
                CostumeSwapAuditOutcome,
                $"Tag=SWAP_COSTUME;Serial={target.Serial};From={target.Enchant};To={resolved.NewImproveA};" +
                $"MaterialSerial={material.Serial};MaterialFrom={material.Enchant};MaterialTo={resolved.NewImproveB}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped costume-swap audit row for character {CharacterId}",
                characterId);

        logger.LogInformation(
            "Character {CharacterId} costume swap applied: target {TargetItemId} <-> material {MaterialItemId}, cost {Cost}",
            characterId, target.ItemId, material.ItemId, resolved.MoneyCost);

        return new EnchantItemResult(EnchantItemOutcome.Applied, CostumeSwapResultCode, resolved.MoneyCost, 0);
    }

    private async ValueTask<EnchantItemResult> EnchantCostumeEnchantAsync(Zone zone, PlayerRuntimeState state,
        int characterId, DateTime attemptUtc, EnchantSlots slots, ItemStack target, ItemStack material,
        CancellationToken cancellationToken)
    {
        var resolved = CostumeImproveResolver.ResolveEnchant(target.Enchant, material.ItemId,
            SystemRandomSource.Instance);

        if (resolved.Outcome == CostumeImproveResolver.CostumeEnchantOutcome.Rejected)
            return AbortAndDisconnect(state, characterId,
                "costume enchant rejected (cap already reached or invalid material)");

        if (state.ContributionPoints < resolved.ContributionPointCost)
            return AbortAndDisconnect(state, characterId,
                "costume enchant insufficient contribution points");

        var remainingMaterialQuantity = material.Quantity - 1;
        var newMaterialStack = remainingMaterialQuantity > 0
            ? material with { Quantity = remainingMaterialQuantity }
            : (ItemStack?)null;

        var succeeded = resolved.Outcome == CostumeImproveResolver.CostumeEnchantOutcome.Success;
        var newTargetStack = target with { Enchant = (byte)resolved.NewImprove };

        var (projectedTarget, projectedMaterial) = ProjectContainers(state, slots, newTargetStack, newMaterialStack);

        try
        {
            await PersistContainersAsync(characterId, -resolved.MoneyCost, slots, projectedTarget, projectedMaterial,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return AbortAfterUncertainPersistence(state, characterId, "costume enchant", ex);
        }

        var inventoryResult = await PostInventoryMirrorAsync(zone, characterId, slots, projectedTarget,
            projectedMaterial, cancellationToken);
        if (inventoryResult.Kind != ZoneCommandResultKind.Applied)
            return AbortAfterDurableMutation(state, characterId, "costume enchant inventory", inventoryResult);

        var newContributionPoints = state.ContributionPoints - resolved.ContributionPointCost;
        var tribeResult = await zone.PostTribeProgressCommandAndWaitForResultAsync(
            new TribeProgressZoneCommand(characterId, newContributionPoints), cancellationToken);
        if (tribeResult.Kind != ZoneCommandResultKind.Applied)
            return AbortAfterDurableMutation(state, characterId, "costume enchant progress", tribeResult);

        state.LastEnchantAttemptUtc = attemptUtc;

        zone.CreditNpcServiceTribeTax(state.Tribe, resolved.MoneyCost);

        if (succeeded && resolved.ReachedCap)
            CenterRelayNoticeLog.LogCostumeEnchantCap(logger, state.Tribe, state.Name, resolved.NewImprove);

        var resultCode = succeeded ? CostumeEnchantSuccessResultCode : CostumeEnchantFailureResultCode;

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(EnchantEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, -(long)resolved.MoneyCost, null, target.ItemId, target.Quantity,
                (byte)resultCode,
                $"Serial={target.Serial};From={target.Enchant};To={resolved.NewImprove};Material={material.ItemId};CpCost={resolved.ContributionPointCost}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped costume-enchant audit row for character {CharacterId}",
                characterId);

        logger.LogInformation(
            "Character {CharacterId} costume enchant applied: target {TargetItemId} outcome {Outcome} -> enchant {NewEnchant}, cost {MoneyCost}/{CpCost}",
            characterId, target.ItemId, resolved.Outcome, resolved.NewImprove, resolved.MoneyCost,
            resolved.ContributionPointCost);

        return new EnchantItemResult(EnchantItemOutcome.Applied, resultCode, resolved.MoneyCost,
            succeeded ? resolved.NewImprove : 0);
    }
}
