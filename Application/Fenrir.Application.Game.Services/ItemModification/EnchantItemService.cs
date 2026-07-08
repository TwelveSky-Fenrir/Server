using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op24, CZ_IMPROVE_ITEM_SEND -- extracted from <see cref="EnchantItemHandler" />, see
///     that handler's remarks. Both the Protection Charm (<see cref="PlayerRuntimeState.ProtectForDestroy" />)
///     and the "sweet potato" Lucky Enchant Scroll (<see cref="PlayerRuntimeState.ImproveItemValue" />) are
///     read from live character state and threaded into <see cref="EnchantResolver.Resolve" /> -- see that
///     type's own remarks for what is and is not yet modeled about the sweet-potato bonus.
/// </summary>
public sealed class EnchantItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IEventLogQueue eventLogQueue,
    ILogger<EnchantItemService> logger)
    : IEnchantItemService
{
    /// <summary>
    ///     game.EventLog.EventCode for an enchant attempt -- the wire opcode (op24) itself, since
    ///     EventLogCategory.Enchant is shared by every item-enhancement opcode in this namespace and EventCode
    ///     is only ever caller-interpreted alongside Category (see game.EventLog.sql's own "app-owned
    ///     numbering scheme" comment).
    /// </summary>
    private const short EnchantEventCode = 24;

    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:2500-2506 (town-zone gate, checked first) ; :2523-2528
    ///     (same-tick anti-spam gate, checked second -- a second attempt within the same server tick drops
    ///     the connection). The same-tick marker (<see cref="PlayerRuntimeState.LastEnchantAttemptUtc" />) is
    ///     stamped unconditionally immediately after this check passes, before any item-slot validation runs,
    ///     matching legacy's own ordering so a same-tick retry is rejected even if the rest of the first
    ///     attempt later fails validation.
    /// </remarks>
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

        // Same-tick anti-spam gate -- see PlayerRuntimeState.LastEnchantAttemptUtc's own remarks and this
        // method's <remarks>. Stamped unconditionally before any further validation, so a same-tick retry is
        // rejected even if the rest of this attempt later fails validation.
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

        // ProtectForDestroy (Protection Charm) and ImproveItemValue ("sweet potato", Lucky Enchant Scroll)
        // both already have real acquisition paths via UseInventoryItemService (op23) -- see
        // EnchantResolver's own remarks for why neither is a guess.
        var resolved = EnchantResolver.Resolve(targetDefinition, target, materialDefinition, luck,
            state.ProtectForDestroy, state.ImproveItemValue, SystemRandomSource.Instance);

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.Rejected)
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected by resolver (target {TargetItemId}, material {MaterialItemId})",
                characterId, target.ItemId, material.ItemId);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        // Wings deduct from CP, never money -- Server/ts25zone/S04_MyWork02.cpp:3222-3450 (the wings-vs-default
        // switch). The non-wing/costume/stellar branches' own fund-sufficiency check was not independently
        // re-confirmed by the contract this was built from (flagged there as an open ambiguity), so this
        // mirrors the same "insufficient funds -> connection dropped" posture the money path already gets
        // from usp_Character_AdjustMoneyAndReplaceContainer's own balance guard below, and the established
        // in-repo precedent for CP-gated actions (NpcShopPolicy.TryResolveCost, CraftItemService's own
        // wing-assembly hasSufficientCp check).
        if (resolved.IsWing && state.ContributionPoints < resolved.Cost)
        {
            logger.LogInformation(
                "Character {CharacterId} enchant rejected: insufficient CP for wing enchant (have {ContributionPoints}, need {Cost})",
                characterId, state.ContributionPoints, resolved.Cost);
            return new EnchantItemResult(EnchantItemOutcome.Rejected, 0, 0, 0);
        }

        // Material is always consumed exactly once regardless of outcome.
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

        // Wings deduct Cost from CP, never money (Server/ts25zone/S04_MyWork02.cpp:3222-3450, the
        // wings-vs-default switch) -- the container write below still runs for both branches (it also
        // carries the material decrement and the target item's enchant-level change), just with a 0 money
        // delta on the wing path.
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

        // Protect/sweet-potato charges are consumed only once the persist above has actually succeeded --
        // same "SQL truth first" ordering as the container/money mutation itself. Both counters are
        // write-behind progression fields (Zone.ApplyTribeProgressCommand), not atomic-with-inventory SQL
        // columns -- see EnchantResolver's own remarks for why the sweet-potato probability bonus itself is
        // not yet applied even though the charge is genuinely consumed and persisted here.
        int? newProtectForDestroy = resolved.ConsumesProtectCharge ? state.ProtectForDestroy - 1 : null;
        int? newImproveItemValue = resolved.ConsumesImproveCharge ? state.ImproveItemValue - 1 : null;

        if (resolved.IsWing)
        {
            // CP is a write-behind progression counter, not an atomic-with-inventory SQL column (same
            // posture CraftItemService's own wing-assembly recipe already uses for the identical resource --
            // see CraftRecipeCatalog.WingAssemblyContributionPointCost's remarks). No tribe-bank credit: the
            // contract's side effects only describe crediting the tribe bank "when money is deducted".
            var newContributionPoints = state.ContributionPoints - resolved.Cost;
            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(characterId, newContributionPoints,
                        ProtectForDestroy: newProtectForDestroy, ImproveItemValue: newImproveItemValue),
                    cancellationToken))
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped CP/charge mirror for character {CharacterId} after wing enchant -- SQL write-behind will retry on next dirty flush",
                    zone.MapId, characterId);
        }
        else
        {
            // Server/ts25zone/S04_MyWork02.cpp:3320-3322 -- AddTribeBankInfo2 credits 1% of the already-charged
            // enchant cost to the paying character's tribe bank immediately after the debit, unconditionally
            // (before the success/fail roll), on the money-cost (non-wing) branch only. Routed through
            // Zone.CreditNpcServiceTribeTax, the pre-existing 1% NPC-service-tax model
            // (WorldState.TribeBankTaxAccumulator) this call site was left unwired for -- see that class's own
            // remarks.
            zone.CreditNpcServiceTribeTax(state.Tribe, resolved.Cost);

            if (newProtectForDestroy is not null || newImproveItemValue is not null)
                if (!await zone.PostTribeProgressCommandAndWaitAsync(
                        new TribeProgressZoneCommand(characterId, ProtectForDestroy: newProtectForDestroy,
                            ImproveItemValue: newImproveItemValue), cancellationToken))
                    logger.LogError(
                        "Zone {MapId} tribe-progress inbox full: dropped protect/sweet-potato charge mirror for character {CharacterId}",
                        zone.MapId, characterId);
        }

        var resultCode = MapResultCode(resolved.Outcome);

        // Server/ts25zone/S04_MyWork02.cpp:3244-3247 (wing) / :3350-3357 (non-wing) -- reaching the enchant
        // cap fires a realm-wide notice via a DIFFERENT relay mechanism than UpgradeCape's own RANKUP notice
        // (mCENTER.U_ZONE_BROADCAST_FOR_CENTER_SEND, sorts 115/2001, not BroadcastNotice's sort 102) -- see
        // CenterRelayNoticeLog's own remarks for why this collapses to a log line rather than a guessed
        // client-facing packet.
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

    /// <summary>ZC_IMPROVE_ITEM_RECV codes: 0 success, 1 fail, 2 destroyed, 3 reset-to-+40, 4 protected.</summary>
    private static int MapResultCode(EnchantResolver.EnchantOutcome outcome)
    {
        return outcome switch
        {
            EnchantResolver.EnchantOutcome.Unsealed => 0,
            EnchantResolver.EnchantOutcome.Success => 0,
            EnchantResolver.EnchantOutcome.Failed => 1,
            EnchantResolver.EnchantOutcome.Destroyed => 2,
            EnchantResolver.EnchantOutcome.ResetToForty => 3,
            EnchantResolver.EnchantOutcome.Protected => 4,
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
