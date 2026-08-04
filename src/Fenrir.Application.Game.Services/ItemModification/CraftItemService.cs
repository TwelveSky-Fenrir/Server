using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class CraftItemService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    WorldDataCache worldData,
    ILogger<CraftItemService> logger)
    : ICraftItemService
{
    private const short JadeUpgradeEventCode = 1;

    private const short AdvancedElixirEventCode = 2;

    private const short StoneMatCombineEventCode = 3;
    private const short MountFusionEventCode = 4;
    private const short WingAssemblyEventCode = 5;
    private const short FeatherTierUpEventCode = 6;
    private const short WingTierRerollEventCode = 7;
    private const short WingFifthTierEventCode = 8;
    private const short DustRecycleEventCode = 9;

    private static readonly CraftFamilyResult RejectedFamilyResult = new(CraftFamilyOutcome.StructuralInvalid, 0, 0, 0,
        null,
        0, 0);

    public async ValueTask<JadeUpgradeResult> ResolveJadeUpgradeAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (!IsValidInventorySlot(page1, index1) || !IsValidInventorySlot(page2, index2) ||
            !AreDistinctSlots(page1, index1, page2, index2))
        {
            logger.LogDebug("Character {CharacterId} craft (jade-upgrade) rejected: invalid slot(s)", characterId);
            return new JadeUpgradeResult(JadeUpgradeOutcome.StructuralInvalid, 0, 0);
        }

        if (!ArePagesAccessible(state, page1, page2))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (jade-upgrade) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return new JadeUpgradeResult(JadeUpgradeOutcome.StructuralInvalid, 0, 0);
        }

        var material1 = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var material2 = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (material1 is not { } m1 || material2 is not { } m2 ||
            !HasValidStoredQuantity(m1) || !HasValidStoredQuantity(m2))
        {
            logger.LogDebug("Character {CharacterId} craft (jade-upgrade) rejected: material slot(s) empty",
                characterId);
            return new JadeUpgradeResult(JadeUpgradeOutcome.StructuralInvalid, 0, 0);
        }

        var resolved = CraftResolver.ResolveJadeUpgrade(m1, m2);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (jade-upgrade) rejected by resolver (materials {M1}/{M2})",
                characterId, m1.ItemId, m2.ItemId);
            return new JadeUpgradeResult(JadeUpgradeOutcome.StructuralInvalid, 0, 0);
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

        await eventLog.LogAsync(JadeUpgradeEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, result.ItemId, result.Quantity, 1, null, cancellationToken);

        var containers = page1 == page2
            ? ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected1))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot((byte)page1, projected1),
                new InventoryContainerSnapshot((byte)page2, projected2));

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, containers, null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (jade) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return new JadeUpgradeResult(JadeUpgradeOutcome.StructuralInvalid, 0, 0);
        }

        logger.LogInformation("Character {CharacterId} craft (jade-upgrade) applied: result item {ResultItemId}",
            characterId, result.ItemId);

        CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, result.ItemId,
            "jade-upgrade");

        return new JadeUpgradeResult(JadeUpgradeOutcome.Applied, result.ItemId, result.Serial);
    }

    public async ValueTask<AdvancedElixirResult> ResolveAdvancedElixirAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (!IsValidInventorySlot(page1, index1))
        {
            logger.LogDebug("Character {CharacterId} craft (advanced-elixir) rejected: invalid slot", characterId);
            return new AdvancedElixirResult(AdvancedElixirOutcome.StructuralInvalid, null, 0, 0, null);
        }

        if (!ArePagesAccessible(state, page1))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (advanced-elixir) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return new AdvancedElixirResult(AdvancedElixirOutcome.StructuralInvalid, null, 0, 0, null);
        }

        var materialStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (materialStack is not { } material || !HasValidStoredQuantity(material))
        {
            logger.LogDebug("Character {CharacterId} craft (advanced-elixir) rejected: material slot empty",
                characterId);
            return new AdvancedElixirResult(AdvancedElixirOutcome.StructuralInvalid, null, 0, 0, null);
        }

        if (!CraftRecipeCatalog.AdvancedElixirBaseItemIds.Contains(material.ItemId) ||
            material.Quantity < CraftRecipeCatalog.AdvancedElixirRequiredQuantity)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (advanced-elixir) rejected: nonconforming material {MaterialItemId} x{Quantity}",
                characterId, material.ItemId, material.Quantity);
            return new AdvancedElixirResult(AdvancedElixirOutcome.StructuralInvalid, null, 0, 0, null);
        }

        var availableResultPlacement = InventoryFreeSlotFinder.Find(state.Inventory, worldData,
            CraftRecipeCatalog.AdvancedElixirResultBaseItemId, state.InventoryDate, GameDate.Today());
        if (availableResultPlacement is null)
        {
            logger.LogDebug("Character {CharacterId} craft (advanced-elixir) rejected: inventory full", characterId);
            return new AdvancedElixirResult(AdvancedElixirOutcome.InventoryFull, null, 0, 0, null);
        }

        var resolved = CraftResolver.ResolveAdvancedElixir(material, true, SystemRandomSource.Instance);

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Rejected)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (advanced-elixir) rejected by resolver (material {MaterialItemId})",
                characterId, material.ItemId);
            return new AdvancedElixirResult(AdvancedElixirOutcome.StructuralInvalid, null, 0, 0, null);
        }

        var freeSlot = resolved.ResultItemId is not null
            ? availableResultPlacement
            : null;

        var resultPage = freeSlot?.Container ?? 0;
        var resultIndex = freeSlot?.Slot ?? 0;
        var projectedMaterialContainer = resolved.RemainingMaterial is { } remainingMaterial
            ? state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, remainingMaterial)
            : state.Inventory.GetContainer((byte)page1).Remove((byte)index1);

        ImmutableArray<InventoryContainerSnapshot> containers;
        ItemStack? newItemStack = null;

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Success)
        {
            var destination = freeSlot!.Value;
            newItemStack = new ItemStack(resolved.ResultItemId!.Value, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                unchecked((int)DateTime.UtcNow.Ticks), destination.X, destination.Y);

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

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Success)
        {
            var created = newItemStack!.Value;
            await eventLog.LogAsync(AdvancedElixirEventCode, EventLogCategory.ItemCreate, accountId, characterId,
                null, null, null, null, null, created.ItemId, created.Quantity, 1, null, cancellationToken);
        }

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, containers, null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (elixir) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return new AdvancedElixirResult(AdvancedElixirOutcome.StructuralInvalid, null, 0, 0, null);
        }

        var outcome = resolved.Outcome == CraftResolver.ElixirOutcome.Success
            ? AdvancedElixirOutcome.Success
            : AdvancedElixirOutcome.Failed;

        logger.LogInformation(
            "Character {CharacterId} craft (advanced-elixir) applied: outcome {Outcome}, resultItem {ResultItemId}",
            characterId, outcome, newItemStack?.ItemId);

        if (newItemStack is { } mintedElixir)
            CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, mintedElixir.ItemId,
                "advanced-elixir");

        return new AdvancedElixirResult(outcome, newItemStack, resultPage, resultIndex, resolved.RemainingMaterial);
    }

    public async ValueTask<CraftFamilyResult> ResolveStoneMatCombineAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2, packet.Page3,
                packet.Index3, packet.Page4, packet.Index4))
        {
            logger.LogDebug("Character {CharacterId} craft (stone-mat-combine) rejected: invalid slot(s)",
                characterId);
            return RejectedFamilyResult;
        }

        if (!ArePagesAccessible(state, packet.Page1, packet.Page2, packet.Page3, packet.Page4))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (stone-mat-combine) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return RejectedFamilyResult;
        }

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } material4 ||
            !HasValidStoredQuantity(material1) || !HasValidStoredQuantity(material2) ||
            !HasValidStoredQuantity(material3) || !HasValidStoredQuantity(material4))
        {
            logger.LogDebug("Character {CharacterId} craft (stone-mat-combine) rejected: material slot(s) empty",
                characterId);
            return RejectedFamilyResult;
        }

        var resolved = CraftResolver.ResolveStoneMatCombine(material1, material2, material3, material4,
            SystemRandomSource.Instance);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (stone-mat-combine) rejected by resolver (materials {M1}/{M2}/{M3}/{M4})",
                characterId, material1.ItemId, material2.ItemId, material3.ItemId, material4.ItemId);
            return RejectedFamilyResult;
        }

        if (!TryCreateResultStack(material1, resolved.ResultItemId, 0, out var resultStack))
            return RejectedFamilyResult;

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);
        working[(byte)packet.Page4] = working[(byte)packet.Page4].Remove((byte)packet.Index4);

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(StoneMatCombineEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resultStack.ItemId, 1, 1, null, cancellationToken);

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (stone-mat) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return RejectedFamilyResult;
        }

        logger.LogInformation(
            "Character {CharacterId} craft (stone-mat-combine) applied: result item {ResultItemId}", characterId,
            resultStack.ItemId);

        CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resultStack.ItemId,
            "stone-mat-combine");

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, 0, resultStack.Serial, null, 0,
            0);
    }

    public async ValueTask<CraftFamilyResult> ResolveMountFusionAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2, packet.Page3,
                packet.Index3, packet.Page4, packet.Index4))
        {
            logger.LogDebug("Character {CharacterId} craft (mount-fusion) rejected: invalid slot(s)", characterId);
            return RejectedFamilyResult;
        }

        if (!ArePagesAccessible(state, packet.Page1, packet.Page2, packet.Page3, packet.Page4))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (mount-fusion) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return RejectedFamilyResult;
        }

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } catalyst ||
            !IsWholeSlotIngredient(material1) || !IsWholeSlotIngredient(material2) ||
            !IsWholeSlotIngredient(material3) || !IsWholeSlotIngredient(catalyst))
        {
            logger.LogDebug("Character {CharacterId} craft (mount-fusion) rejected: material slot(s) empty",
                characterId);
            return RejectedFamilyResult;
        }

        var resolved = CraftResolver.ResolveMountFusion(packet.Sort, material1.ItemId, material2.ItemId,
            material3.ItemId, catalyst.ItemId, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (mount-fusion) rejected by resolver (materials {M1}/{M2}/{M3}, catalyst {Catalyst})",
                characterId, material1.ItemId, material2.ItemId, material3.ItemId, catalyst.ItemId);
            return RejectedFamilyResult;
        }

        var quantity = resolved.Outcome == CraftResolver.MountFusionOutcome.DustConsolation
            ? resolved.ResultQuantity
            : 0;
        if (!TryCreateResultStack(material1, resolved.ResultItemId, quantity, out var resultStack))
            return RejectedFamilyResult;

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);
        working[(byte)packet.Page4] = working[(byte)packet.Page4].Remove((byte)packet.Index4);

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(MountFusionEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resultStack.ItemId, Math.Max(quantity, 1), 1, null, cancellationToken);

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (mount-fusion) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return RejectedFamilyResult;
        }

        logger.LogInformation(
            "Character {CharacterId} craft (mount-fusion) applied: result item {ResultItemId} x{Quantity}",
            characterId, resultStack.ItemId, quantity);

        CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resultStack.ItemId,
            "mount-fusion");

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, quantity, resultStack.Serial,
            null, 0, 0);
    }

    public async ValueTask<CraftFamilyResult> ResolveWingAssemblyAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2, packet.Page3,
                packet.Index3, packet.Page4, packet.Index4))
        {
            logger.LogDebug("Character {CharacterId} craft (wing-assembly) rejected: invalid slot(s)", characterId);
            return RejectedFamilyResult;
        }

        if (!ArePagesAccessible(state, packet.Page1, packet.Page2, packet.Page3, packet.Page4))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (wing-assembly) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return RejectedFamilyResult;
        }

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } catalyst ||
            !IsWholeSlotIngredient(material1) || !IsWholeSlotIngredient(material2) ||
            !IsWholeSlotIngredient(material3) || !IsWholeSlotIngredient(catalyst))
        {
            logger.LogDebug("Character {CharacterId} craft (wing-assembly) rejected: material slot(s) empty",
                characterId);
            return RejectedFamilyResult;
        }

        var isTownZone = CraftRecipeCatalog.WingAssemblyTownMapIds.Contains(zone.MapId);
        var hasSufficientCp = state.ContributionPoints >= CraftRecipeCatalog.WingAssemblyContributionPointCost;

        var resolved = CraftResolver.ResolveWingAssembly(isTownZone, hasSufficientCp, material1.ItemId,
            material2.ItemId, material3.ItemId, catalyst.ItemId, state.PreviousTribe, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (wing-assembly) rejected by resolver (isTownZone={IsTownZone}, hasSufficientCp={HasSufficientCp}, materials {M1}/{M2}/{M3}, catalyst {Catalyst})",
                characterId, isTownZone, hasSufficientCp, material1.ItemId, material2.ItemId, material3.ItemId,
                catalyst.ItemId);
            return RejectedFamilyResult;
        }

        var newContributionPoints = state.ContributionPoints - CraftRecipeCatalog.WingAssemblyContributionPointCost;

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);

        int resultItemId;
        if (resolved.Outcome == CraftResolver.WingAssemblyOutcome.Assembled)
        {
            if (!TryCreateResultStack(material1, resolved.ResultItemId, 0, out var resultStack))
                return RejectedFamilyResult;
            working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);

            EnsureContainer(working, state, (byte)packet.Page2);
            EnsureContainer(working, state, (byte)packet.Page3);
            EnsureContainer(working, state, (byte)packet.Page4);
            working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
            working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);
            working[(byte)packet.Page4] = working[(byte)packet.Page4].Remove((byte)packet.Index4);

            resultItemId = resultStack.ItemId;
        }
        else
        {
            working[(byte)packet.Page1] = working[(byte)packet.Page1].Remove((byte)packet.Index1);
            resultItemId = 0;
        }

        await PersistContainersAsync(characterId, working, cancellationToken);

        if (resultItemId != 0)
            await eventLog.LogAsync(WingAssemblyEventCode, EventLogCategory.ItemCreate, accountId, characterId,
                null, null, null, null, null, resultItemId, 1, 1, null, cancellationToken);

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (wing-assembly) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return RejectedFamilyResult;
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, newContributionPoints), cancellationToken))
        {
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped CP mirror for character {CharacterId} after craft (wing-assembly)",
                zone.MapId, characterId);
            return RejectedFamilyResult;
        }

        logger.LogInformation(
            "Character {CharacterId} craft (wing-assembly) applied: outcome {Outcome}, result item {ResultItemId}, CP now {NewContributionPoints}",
            characterId, resolved.Outcome, resultItemId, newContributionPoints);

        if (resultItemId != 0)
            CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resultItemId,
                "wing-assembly");

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultItemId, 0, material1.Serial, null, 0, 0);
    }

    public async ValueTask<CraftFamilyResult> ResolveFeatherTierUpAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2, packet.Page3,
                packet.Index3, packet.Page4, packet.Index4))
        {
            logger.LogDebug("Character {CharacterId} craft (feather-tier-up) rejected: invalid slot(s)",
                characterId);
            return RejectedFamilyResult;
        }

        if (!ArePagesAccessible(state, packet.Page1, packet.Page2, packet.Page3, packet.Page4))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (feather-tier-up) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return RejectedFamilyResult;
        }

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } material4 ||
            !HasValidStoredQuantity(material1) || !HasValidStoredQuantity(material2) ||
            !HasValidStoredQuantity(material3) || !HasValidStoredQuantity(material4))
        {
            logger.LogDebug("Character {CharacterId} craft (feather-tier-up) rejected: material slot(s) empty",
                characterId);
            return RejectedFamilyResult;
        }

        var resolved = CraftResolver.ResolveFeatherTierUp(material1.ItemId, material1.Quantity, material2.ItemId,
            material2.Quantity, material3.ItemId, material3.Quantity, material4.ItemId, material4.Quantity,
            SystemRandomSource.Instance);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (feather-tier-up) rejected by resolver (materials {M1}/{M2}/{M3}/{M4})",
                characterId, material1.ItemId, material2.ItemId, material3.ItemId, material4.ItemId);
            return RejectedFamilyResult;
        }

        if (!TryCreateResultStack(material1, resolved.ResultItemId, 0, out var resultStack))
            return RejectedFamilyResult;

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);
        working[(byte)packet.Page4] = working[(byte)packet.Page4].Remove((byte)packet.Index4);

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(FeatherTierUpEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resultStack.ItemId, 1, 1, null, cancellationToken);

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (feather-tier-up) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return RejectedFamilyResult;
        }

        logger.LogInformation(
            "Character {CharacterId} craft (feather-tier-up) applied: result item {ResultItemId}",
            characterId, resultStack.ItemId);

        CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resultStack.ItemId,
            "feather-tier-up");

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, 0, resultStack.Serial, null, 0,
            0);
    }

    public async ValueTask<CraftFamilyResult> ResolveWingTierRerollAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2, packet.Page3,
                packet.Index3, packet.Page4, packet.Index4))
        {
            logger.LogDebug("Character {CharacterId} craft (wing-tier-reroll) rejected: invalid slot(s)",
                characterId);
            return RejectedFamilyResult;
        }

        if (!ArePagesAccessible(state, packet.Page1, packet.Page2, packet.Page3, packet.Page4))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (wing-tier-reroll) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return RejectedFamilyResult;
        }

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } catalyst ||
            !IsWholeSlotIngredient(material1) || !IsWholeSlotIngredient(material2) ||
            !IsWholeSlotIngredient(material3) || !HasValidStoredQuantity(catalyst))
        {
            logger.LogDebug("Character {CharacterId} craft (wing-tier-reroll) rejected: material slot(s) empty",
                characterId);
            return RejectedFamilyResult;
        }

        var resolved = CraftResolver.ResolveWingTierReroll(material1.ItemId, material2.ItemId, material3.ItemId,
            catalyst.ItemId, catalyst.Quantity, state.PreviousTribe, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (wing-tier-reroll) rejected by resolver (materials {M1}/{M2}/{M3}, catalyst {Catalyst})",
                characterId, material1.ItemId, material2.ItemId, material3.ItemId, catalyst.ItemId);
            return RejectedFamilyResult;
        }

        var quantity = resolved.Outcome == CraftResolver.WingTierRerollOutcome.DustConsolation
            ? CraftRecipeCatalog.WingTierRerollFailureDustQuantity
            : 0;
        if (!TryCreateResultStack(material1, resolved.ResultItemId, quantity, out var resultStack))
            return RejectedFamilyResult;

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);

        var remainingCatalystQuantity = catalyst.Quantity - 1;
        working[(byte)packet.Page4] = remainingCatalystQuantity > 0
            ? working[(byte)packet.Page4].SetItem((byte)packet.Index4,
                catalyst with { Quantity = remainingCatalystQuantity })
            : working[(byte)packet.Page4].Remove((byte)packet.Index4);

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(WingTierRerollEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resultStack.ItemId, Math.Max(quantity, 1), 1, null, cancellationToken);

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (wing-tier-reroll) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return RejectedFamilyResult;
        }

        logger.LogInformation(
            "Character {CharacterId} craft (wing-tier-reroll) applied: result item {ResultItemId} x{Quantity}",
            characterId, resultStack.ItemId, quantity);

        CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resultStack.ItemId,
            "wing-tier-reroll");

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, quantity, resultStack.Serial,
            null, 0, 0);
    }

    public async ValueTask<CraftFamilyResult> ResolveWingFifthTierAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4) ||
            !AreDistinctSlots(packet.Page1, packet.Index1, packet.Page2, packet.Index2, packet.Page3,
                packet.Index3, packet.Page4, packet.Index4))
        {
            logger.LogDebug("Character {CharacterId} craft (wing-fifth-tier) rejected: invalid slot(s)",
                characterId);
            return RejectedFamilyResult;
        }

        if (!ArePagesAccessible(state, packet.Page1, packet.Page2, packet.Page3, packet.Page4))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (wing-fifth-tier) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return RejectedFamilyResult;
        }

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } catalyst ||
            !IsWholeSlotIngredient(material1) || !IsWholeSlotIngredient(material2) ||
            !IsWholeSlotIngredient(material3) || !IsWholeSlotIngredient(catalyst))
        {
            logger.LogDebug("Character {CharacterId} craft (wing-fifth-tier) rejected: material slot(s) empty",
                characterId);
            return RejectedFamilyResult;
        }

        var resolved = CraftResolver.ResolveWingFifthTier(packet.Sort, material1.ItemId, material2.ItemId,
            material3.ItemId, catalyst.ItemId, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (wing-fifth-tier) rejected by resolver (sort {Sort}, materials {M1}/{M2}/{M3}, catalyst {Catalyst})",
                characterId, packet.Sort, material1.ItemId, material2.ItemId, material3.ItemId, catalyst.ItemId);
            return RejectedFamilyResult;
        }

        var quantity = resolved.Outcome == CraftResolver.WingFifthTierOutcome.DustConsolation
            ? CraftResolver.WingTierFailureDustQuantity(packet.Sort)
            : 0;
        if (!TryCreateResultStack(material1, resolved.ResultItemId, quantity, out var resultStack))
            return RejectedFamilyResult;

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);
        working[(byte)packet.Page4] = working[(byte)packet.Page4].Remove((byte)packet.Index4);

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(WingFifthTierEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resultStack.ItemId, Math.Max(quantity, 1), 1, null, cancellationToken);

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (wing-fifth-tier) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return RejectedFamilyResult;
        }

        logger.LogInformation(
            "Character {CharacterId} craft (wing-fifth-tier) applied: result item {ResultItemId} x{Quantity}",
            characterId, resultStack.ItemId, quantity);

        CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resultStack.ItemId,
            "wing-fifth-tier");

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, quantity, resultStack.Serial,
            null, 0, 0);
    }

    public async ValueTask<CraftFamilyResult> ResolveDustRecycleAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1))
        {
            logger.LogDebug("Character {CharacterId} craft (dust-recycle) rejected: invalid slot", characterId);
            return RejectedFamilyResult;
        }

        if (!ArePagesAccessible(state, packet.Page1))
        {
            logger.LogDebug(
                "Character {CharacterId} craft (dust-recycle) rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return RejectedFamilyResult;
        }

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material ||
            !HasValidStoredQuantity(material))
        {
            logger.LogDebug("Character {CharacterId} craft (dust-recycle) rejected: material slot empty",
                characterId);
            return RejectedFamilyResult;
        }

        var resolved = CraftResolver.ResolveDustRecycle(packet.Sort, material.ItemId, material.Quantity,
            state.PreviousTribe, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft (dust-recycle) rejected by resolver (sort {Sort}, material {MaterialItemId} x{Quantity})",
                characterId, packet.Sort, material.ItemId, material.Quantity);
            return RejectedFamilyResult;
        }

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);

        if (!TryCreateResultStack(material, resolved.ResultItemId, 0, out var resultStack))
            return RejectedFamilyResult;
        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(DustRecycleEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resolved.ResultItemId, 1, 1, null, cancellationToken);

        if ((await zone.PostInventoryCommandAndWaitForResultAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken)).Kind !=
            ZoneCommandResultKind.Applied)
        {
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (dust-recycle) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
            return RejectedFamilyResult;
        }

        logger.LogInformation(
            "Character {CharacterId} craft (dust-recycle) applied: result {ResultItemId}", characterId,
            resultStack.ItemId);

        CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resultStack.ItemId,
            "dust-recycle");

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, 0, resultStack.Serial, null, 0,
            0);
    }

    private static void EnsureContainer(Dictionary<byte, ImmutableDictionary<byte, ItemStack>> working,
        PlayerRuntimeState state, byte page)
    {
        if (!working.ContainsKey(page))
            working[page] = state.Inventory.GetContainer(page);
    }

    private async ValueTask PersistContainersAsync(int characterId,
        Dictionary<byte, ImmutableDictionary<byte, ItemStack>> working, CancellationToken cancellationToken)
    {
        var pages = working.Keys.ToArray();
        if (pages.Length == 1)
            await characters.ReplaceContainerAsync(characterId, pages[0], ToTvps(working[pages[0]]),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, pages[0], ToTvps(working[pages[0]]), pages[1],
                ToTvps(working[pages[1]]), cancellationToken);
    }

    private static ImmutableArray<InventoryContainerSnapshot> SnapshotContainers(
        Dictionary<byte, ImmutableDictionary<byte, ItemStack>> working)
    {
        var builder = ImmutableArray.CreateBuilder<InventoryContainerSnapshot>(working.Count);
        foreach (var (page, container) in working)
            builder.Add(new InventoryContainerSnapshot(page, container));
        return builder.MoveToImmutable();
    }

    private static bool IsValidInventorySlot(int page, int index)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, index);
    }

    private bool HasValidStoredQuantity(ItemStack stack)
    {
        return worldData.ItemsById.TryGetValue(stack.ItemId, out var definition) &&
               ItemQuantityPolicy.IsWithinLegalRange(definition.Item.Sort, stack.Quantity);
    }

    private bool IsWholeSlotIngredient(ItemStack stack)
    {
        return worldData.ItemsById.TryGetValue(stack.ItemId, out var definition) &&
               ItemQuantityPolicy.IsWithinLegalRange(definition.Item.Sort, stack.Quantity) &&
               (!ItemQuantityPolicy.IsStackableSort(definition.Item.Sort) ||
                stack.Quantity == ItemQuantityPolicy.MinStackQuantity);
    }

    private bool TryCreateResultStack(ItemStack source, int itemId, int recipeQuantity, out ItemStack result)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var definition))
        {
            result = default;
            return false;
        }

        var quantity = ItemQuantityPolicy.IsStackableSort(definition.Item.Sort)
            ? recipeQuantity > 0 ? recipeQuantity : ItemQuantityPolicy.MinStackQuantity
            : recipeQuantity;
        if (!ItemQuantityPolicy.IsWithinLegalRange(definition.Item.Sort, quantity))
        {
            result = default;
            return false;
        }

        result = source with
        {
            ItemId = itemId, Quantity = quantity, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
        };
        return true;
    }

    private static bool AreDistinctSlots(int page1, int index1, int page2, int index2)
    {
        return page1 != page2 || index1 != index2;
    }

    private static bool AreDistinctSlots(int page1, int index1, int page2, int index2, int page3, int index3,
        int page4, int index4)
    {
        return AreDistinctSlots(page1, index1, page2, index2) &&
               AreDistinctSlots(page1, index1, page3, index3) &&
               AreDistinctSlots(page1, index1, page4, index4) &&
               AreDistinctSlots(page2, index2, page3, index3) &&
               AreDistinctSlots(page2, index2, page4, index4) &&
               AreDistinctSlots(page3, index3, page4, index4);
    }

    private static bool ArePagesAccessible(PlayerRuntimeState state, int page1,
        int page2 = ContainerMatrix.InventoryPage0, int page3 = ContainerMatrix.InventoryPage0,
        int page4 = ContainerMatrix.InventoryPage0)
    {
        var today = GameDate.Today();
        return RentedInventoryPageGate.IsPageAccessible(page1, state.InventoryDate, today) &&
               RentedInventoryPageGate.IsPageAccessible(page2, state.InventoryDate, today) &&
               RentedInventoryPageGate.IsPageAccessible(page3, state.InventoryDate, today) &&
               RentedInventoryPageGate.IsPageAccessible(page4, state.InventoryDate, today);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
