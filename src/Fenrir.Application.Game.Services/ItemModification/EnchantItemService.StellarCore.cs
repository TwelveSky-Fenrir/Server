using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed partial class EnchantItemService
{
    private const int StellarCoreResultCode = 20;

    private async ValueTask<EnchantItemResult> EnchantStellarCoreAsync(Zone zone, PlayerRuntimeState state,
        int characterId, DateTime attemptUtc, EnchantSlots slots, ItemStack target, ItemDefinition targetDefinition,
        ItemStack material,
        ItemDefinition materialDefinition, CancellationToken cancellationToken)
    {
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var premiumActive = state.PremiumExpireUtc >= nowUnixSeconds;

        var resolved = StellarCoreResolver.Resolve(targetDefinition, materialDefinition, premiumActive);

        if (resolved.Outcome == StellarCoreResolver.StellarCoreOutcome.Rejected)
            return AbortAndDisconnect(state, characterId,
                "stellar-core merge rejected (top-tier item or mismatched material)");

        if (!worldData.ItemsById.ContainsKey(resolved.NewTargetItemId))
            return AbortAndDisconnect(state, characterId, "stellar-core result item is missing");

        var newTargetStack = target with { ItemId = resolved.NewTargetItemId };

        var (projectedTarget, projectedMaterial) = ProjectContainers(state, slots, newTargetStack, null);

        try
        {
            await PersistContainersAsync(characterId, -resolved.Cost, slots, projectedTarget, projectedMaterial,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return AbortAfterUncertainPersistence(state, characterId, "stellar-core merge", ex);
        }

        var inventoryResult = await PostInventoryMirrorAsync(zone, characterId, slots, projectedTarget,
            projectedMaterial, cancellationToken);
        if (inventoryResult.Kind != ZoneCommandResultKind.Applied)
            return AbortAfterDurableMutation(state, characterId, "stellar-core inventory", inventoryResult);

        state.LastEnchantAttemptUtc = attemptUtc;

        zone.CreditNpcServiceTribeTax(state.Tribe, resolved.Cost);

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(EnchantEventCode, (byte)EventLogCategory.Enchant, null,
                characterId, null, null, null, -(long)resolved.Cost, null, target.ItemId, target.Quantity,
                StellarCoreResultCode,
                $"Serial={target.Serial};From={target.ItemId};To={resolved.NewTargetItemId};Material={material.ItemId}",
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped stellar-core audit row for character {CharacterId}",
                characterId);

        logger.LogInformation(
            "Character {CharacterId} stellar-core merge applied: {TargetItemId} -> {NewTargetItemId}, cost {Cost}",
            characterId, target.ItemId, resolved.NewTargetItemId, resolved.Cost);

        return new EnchantItemResult(EnchantItemOutcome.Applied, StellarCoreResultCode, resolved.Cost,
            resolved.NewTargetItemId);
    }
}
