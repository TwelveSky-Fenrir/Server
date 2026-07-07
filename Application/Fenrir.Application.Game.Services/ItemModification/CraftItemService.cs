using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op29, CZ_MAKE_ITEM_SEND -- extracted from <see cref="CraftItemHandler" />, see that
///     handler's remarks.
/// </summary>
public sealed class CraftItemService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<CraftItemService> logger)
    : ICraftItemService
{
    /// <summary>
    ///     game.EventLog.EventCode for a Jade Upgrade craft (MK_* sort <see cref="CraftRecipeCatalog.JadeUpgradeSort" />)
    ///     minting a Red Jade -- scoped independently within <see cref="EventLogCategory.ItemCreate" />; EventCode
    ///     is only ever caller-interpreted alongside its Category (see game.EventLog.sql's own "app-owned
    ///     numbering scheme" comment), so this does not collide with any other family's numbering, including
    ///     <see cref="AdvancedElixirEventCode" /> below.
    /// </summary>
    private const short JadeUpgradeEventCode = 1;

    /// <summary>
    ///     game.EventLog.EventCode for an Advanced Elixir craft (MK_* sort
    ///     <see cref="CraftRecipeCatalog.AdvancedElixirSort" />) minting a random [801,806] item, scoped
    ///     independently within <see cref="EventLogCategory.ItemCreate" />.
    /// </summary>
    private const short AdvancedElixirEventCode = 2;

    private const short StoneMatCombineEventCode = 3;
    private const short MountFusionEventCode = 4;
    private const short WingAssemblyEventCode = 5;
    private const short FeatherTierUpEventCode = 6;
    private const short WingTierRerollEventCode = 7;
    private const short WingFifthTierEventCode = 8;
    private const short DustRecycleEventCode = 9;

    private static readonly CraftFamilyResult RejectedFamilyResult = new(CraftFamilyOutcome.Rejected, 0, 0, 0, null,
        0, 0);

    private static readonly byte[] InventoryPagesInScanOrder =
        [ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1];

    public async ValueTask<JadeUpgradeResult> ResolveJadeUpgradeAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        if (!IsValidInventorySlot(page1, index1) || !IsValidInventorySlot(page2, index2))
            return new JadeUpgradeResult(JadeUpgradeOutcome.Rejected, 0, 0);

        var material1 = state.Inventory.GetSlot((byte)page1, (byte)index1);
        var material2 = state.Inventory.GetSlot((byte)page2, (byte)index2);

        if (material1 is not { } m1 || material2 is not { } m2)
            return new JadeUpgradeResult(JadeUpgradeOutcome.Rejected, 0, 0);

        var resolved = CraftResolver.ResolveJadeUpgrade(m1, m2);
        if (!resolved.Succeeded)
            return new JadeUpgradeResult(JadeUpgradeOutcome.Rejected, 0, 0);

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

        // Logged only once the container replace(s) above have durably committed -- an ItemCreate row must
        // never assert a mint that the DB write didn't actually persist.
        await eventLog.LogAsync(JadeUpgradeEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, result.ItemId, result.Quantity, 1, null, cancellationToken);

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

        return new JadeUpgradeResult(JadeUpgradeOutcome.Applied, result.ItemId, result.Serial);
    }

    public async ValueTask<AdvancedElixirResult> ResolveAdvancedElixirAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (!IsValidInventorySlot(page1, index1))
            return new AdvancedElixirResult(AdvancedElixirOutcome.Rejected, null, 0, 0, null);

        var materialStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (materialStack is not { } material)
            return new AdvancedElixirResult(AdvancedElixirOutcome.Rejected, null, 0, 0, null);

        // Free-slot scan happens before rolling, while the material's own slot is still occupied, so it can
        // never be picked as its own destination.
        var hasFreeSlot = TryFindEmptySlot(state, out var resultPage, out var resultIndex);

        var resolved = CraftResolver.ResolveAdvancedElixir(material, hasFreeSlot, SystemRandomSource.Instance);

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Rejected)
            return new AdvancedElixirResult(AdvancedElixirOutcome.Rejected, null, 0, 0, null);

        var projectedMaterialContainer = resolved.RemainingMaterial is { } remainingMaterial
            ? state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, remainingMaterial)
            : state.Inventory.GetContainer((byte)page1).Remove((byte)index1);

        ImmutableArray<InventoryContainerSnapshot> containers;
        ItemStack? newItemStack = null;

        if (resolved.Outcome == CraftResolver.ElixirOutcome.Success)
        {
            newItemStack = new ItemStack(resolved.ResultItemId!.Value, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                unchecked((int)DateTime.UtcNow.Ticks));

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

        // Only the roll's success path actually mints a new item -- the 80% failure case still consumes the
        // material (see CraftResolver's own remarks) but creates nothing, so it gets no ItemCreate row.
        if (resolved.Outcome == CraftResolver.ElixirOutcome.Success)
        {
            var created = newItemStack!.Value;
            await eventLog.LogAsync(AdvancedElixirEventCode, EventLogCategory.ItemCreate, accountId, characterId,
                null, null, null, null, null, created.ItemId, created.Quantity, 1, null, cancellationToken);
        }

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (elixir) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        var outcome = resolved.Outcome == CraftResolver.ElixirOutcome.Success
            ? AdvancedElixirOutcome.Success
            : AdvancedElixirOutcome.Failed;

        return new AdvancedElixirResult(outcome, newItemStack, resultPage, resultIndex, resolved.RemainingMaterial);
    }

    /// <summary>
    ///     MK_MATS_01019 -- 4x item 1019 -&gt; 1 random stone-mat item, see
    ///     <see cref="CraftResolver.ResolveStoneMatCombine" />.
    /// </summary>
    public async ValueTask<CraftFamilyResult> ResolveStoneMatCombineAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4))
            return RejectedFamilyResult;

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } material4)
            return RejectedFamilyResult;

        var resolved = CraftResolver.ResolveStoneMatCombine(material1, material2, material3, material4,
            SystemRandomSource.Instance);
        if (!resolved.Succeeded)
            return RejectedFamilyResult;

        var resultStack = material1 with
        {
            ItemId = resolved.ResultItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
        };

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

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (stone-mat) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, 0, resultStack.Serial, null, 0,
            0);
    }

    /// <summary>MK_ANIMAL_NUM_1/2 -- see <see cref="CraftResolver.ResolveMountFusion" />.</summary>
    public async ValueTask<CraftFamilyResult> ResolveMountFusionAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4))
            return RejectedFamilyResult;

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } catalyst)
            return RejectedFamilyResult;

        var resolved = CraftResolver.ResolveMountFusion(packet.Sort, material1.ItemId, material2.ItemId,
            material3.ItemId, catalyst.ItemId, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
            return RejectedFamilyResult;

        // Dust consolation is a genuine stack (quantity 3 or 9); the real mount win is always a single fresh
        // unit (quantity 0) -- :4602-4630.
        var quantity = resolved.Outcome == CraftResolver.MountFusionOutcome.DustConsolation
            ? resolved.ResultQuantity
            : 0;
        var resultStack = material1 with
        {
            ItemId = resolved.ResultItemId, Quantity = quantity, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
        };

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

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (mount-fusion) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, quantity, resultStack.Serial,
            null, 0, 0);
    }

    /// <summary>MK_WING_0 -- see <see cref="CraftResolver.ResolveWingAssembly" />.</summary>
    public async ValueTask<CraftFamilyResult> ResolveWingAssemblyAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4))
            return RejectedFamilyResult;

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } catalyst)
            return RejectedFamilyResult;

        var isTownZone = CraftRecipeCatalog.WingAssemblyTownMapIds.Contains(zone.MapId);
        var hasSufficientCp = state.ContributionPoints >= CraftRecipeCatalog.WingAssemblyContributionPointCost;

        var resolved = CraftResolver.ResolveWingAssembly(isTownZone, hasSufficientCp, material1.ItemId,
            material2.ItemId, material3.ItemId, catalyst.ItemId, state.PreviousTribe, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
            return RejectedFamilyResult;

        // The 50 CP cost is paid before the roll, win or lose -- :5140.
        var newContributionPoints = state.ContributionPoints - CraftRecipeCatalog.WingAssemblyContributionPointCost;

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);

        int resultItemId;
        if (resolved.Outcome == CraftResolver.WingAssemblyOutcome.Assembled)
        {
            var resultStack = material1 with
            {
                ItemId = resolved.ResultItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
            };
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
            // Destroyed: only slot1 is cleared -- slots 2-4 (the other 2 feathers + the catalyst gem) are
            // left completely untouched, an asymmetry directly confirmed at S04_MyWork02.cpp:5188-5199 (the
            // legacy source itself only calls wClearInv(1) on this branch, not a Fenrir-side simplification).
            working[(byte)packet.Page1] = working[(byte)packet.Page1].Remove((byte)packet.Index1);
            resultItemId = 0;
        }

        await PersistContainersAsync(characterId, working, cancellationToken);

        if (resultItemId != 0)
            await eventLog.LogAsync(WingAssemblyEventCode, EventLogCategory.ItemCreate, accountId, characterId,
                null, null, null, null, null, resultItemId, 1, 1, null, cancellationToken);

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (wing-assembly) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, newContributionPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped CP mirror for character {CharacterId} after craft (wing-assembly)",
                zone.MapId, characterId);

        // material1.Serial is preserved by the `with` expression above in both branches (only ItemId/Quantity
        // change), matching the legacy's own tValue[5] capture before either branch runs.
        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultItemId, 0, material1.Serial, null, 0, 0);
    }

    /// <summary>MK_WING_1/MK_WING_3 -- see <see cref="CraftResolver.ResolveFeatherTierUp" />.</summary>
    public async ValueTask<CraftFamilyResult> ResolveFeatherTierUpAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1))
            return RejectedFamilyResult;

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material)
            return RejectedFamilyResult;

        var resolved = CraftResolver.ResolveFeatherTierUp(packet.Sort, material.ItemId, material.Quantity);
        if (!resolved.Succeeded)
            return RejectedFamilyResult;

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);

        ItemStack? grantedItem = null;
        byte grantedPage = 0;
        byte grantedIndex = 0;
        int resultItemId;
        int resultQuantity;
        int serial;

        if (material.Quantity == CraftRecipeCatalog.FeatherTierUpRequiredQuantity)
        {
            // Exact quantity: slot1 converts in place into a single fresh unit of the gained feather.
            var resultStack = material with
            {
                ItemId = resolved.ResultItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
            };
            working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);
            resultItemId = resultStack.ItemId;
            resultQuantity = 0;
            serial = resultStack.Serial;
        }
        else
        {
            // Over-quantity: slot1 keeps the ORIGINAL feather with 10 units deducted; the gained feather is
            // granted as a brand-new single-unit stack in a free slot -- :5310-5335.
            if (!TryFindEmptySlot(state, out grantedPage, out grantedIndex))
                return RejectedFamilyResult;

            var remainingMaterial = material with
            {
                Quantity = material.Quantity - CraftRecipeCatalog.FeatherTierUpRequiredQuantity
            };
            working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, remainingMaterial);

            var newStack = new ItemStack(resolved.ResultItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                unchecked((int)DateTime.UtcNow.Ticks));
            grantedItem = newStack;

            if (grantedPage == packet.Page1)
            {
                working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem(grantedIndex, newStack);
            }
            else
            {
                EnsureContainer(working, state, grantedPage);
                working[grantedPage] = working[grantedPage].SetItem(grantedIndex, newStack);
            }

            resultItemId = remainingMaterial.ItemId;
            resultQuantity = remainingMaterial.Quantity;
            serial = remainingMaterial.Serial;
        }

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(FeatherTierUpEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resolved.ResultItemId, 1, 1, null, cancellationToken);

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (feather-tier-up) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultItemId, resultQuantity, serial, grantedItem,
            grantedPage, grantedIndex);
    }

    /// <summary>MK_WING_2 -- see <see cref="CraftResolver.ResolveWingTierReroll" />.</summary>
    public async ValueTask<CraftFamilyResult> ResolveWingTierRerollAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4))
            return RejectedFamilyResult;

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } catalyst)
            return RejectedFamilyResult;

        var resolved = CraftResolver.ResolveWingTierReroll(material1.ItemId, material2.ItemId, material3.ItemId,
            catalyst.ItemId, catalyst.Quantity, state.PreviousTribe, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
            return RejectedFamilyResult;

        var quantity = resolved.Outcome == CraftResolver.WingTierRerollOutcome.DustConsolation
            ? CraftRecipeCatalog.WingTierRerollFailureDustQuantity
            : 0;
        var resultStack = material1 with
        {
            ItemId = resolved.ResultItemId, Quantity = quantity, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
        };

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);

        // The catalyst is only partially consumed (1 unit), unlike the other 3 slots -- :5398.
        var remainingCatalystQuantity = catalyst.Quantity - 1;
        working[(byte)packet.Page4] = remainingCatalystQuantity > 0
            ? working[(byte)packet.Page4].SetItem((byte)packet.Index4,
                catalyst with { Quantity = remainingCatalystQuantity })
            : working[(byte)packet.Page4].Remove((byte)packet.Index4);

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(WingTierRerollEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resultStack.ItemId, Math.Max(quantity, 1), 1, null, cancellationToken);

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (wing-tier-reroll) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, quantity, resultStack.Serial,
            null, 0, 0);
    }

    /// <summary>
    ///     MK_WING_5/MK_WING_6 -- see <see cref="CraftResolver.ResolveWingFifthTier" />. For sort 46, every
    ///     material identity/quantity check is skipped by the resolver (legacy validation bypass, see
    ///     <see cref="CraftRecipeCatalog.WingSixthTierUnvalidatedSort" />'s remarks) -- this method still
    ///     requires all 4 referenced slots to be occupied (a baseline structural invariant this whole service
    ///     applies everywhere, not a recipe-specific check), so it does not reproduce the literal legacy
    ///     behavior of succeeding from 4 completely empty slots, only "any 4 occupied slots regardless of item
    ///     identity/quantity". Flagged for fenrir-security-hardening-engineer / cpp-security-debt-auditor
    ///     before deciding whether the full literal behavior should ever be reproduced.
    /// </summary>
    public async ValueTask<CraftFamilyResult> ResolveWingFifthTierAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1) || !IsValidInventorySlot(packet.Page2, packet.Index2) ||
            !IsValidInventorySlot(packet.Page3, packet.Index3) || !IsValidInventorySlot(packet.Page4, packet.Index4))
            return RejectedFamilyResult;

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material1 ||
            state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2) is not { } material2 ||
            state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3) is not { } material3 ||
            state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4) is not { } catalyst)
            return RejectedFamilyResult;

        var resolved = CraftResolver.ResolveWingFifthTier(packet.Sort, material1.ItemId, material2.ItemId,
            material3.ItemId, catalyst.ItemId, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
            return RejectedFamilyResult;

        var quantity = resolved.Outcome == CraftResolver.WingFifthTierOutcome.DustConsolation
            ? CraftRecipeCatalog.WingFifthFailureDustQuantity
            : 0;
        var resultStack = material1 with
        {
            ItemId = resolved.ResultItemId, Quantity = quantity, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
        };

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

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (wing-fifth-tier) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultStack.ItemId, quantity, resultStack.Serial,
            null, 0, 0);
    }

    /// <summary>MK_DUST_WING/CLOAK/ANIMAL/PET1/PET2 -- see <see cref="CraftResolver.ResolveDustRecycle" />.</summary>
    public async ValueTask<CraftFamilyResult> ResolveDustRecycleAsync(CraftItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidInventorySlot(packet.Page1, packet.Index1))
            return RejectedFamilyResult;

        if (state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1) is not { } material)
            return RejectedFamilyResult;

        var resolved = CraftResolver.ResolveDustRecycle(packet.Sort, material.ItemId, material.Quantity,
            state.PreviousTribe, SystemRandomSource.Instance);
        if (!resolved.Succeeded)
            return RejectedFamilyResult;

        var threshold = CraftResolver.DustRecycleThreshold(packet.Sort);

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);

        ItemStack? grantedItem = null;
        byte grantedPage = 0;
        byte grantedIndex = 0;
        int resultItemId;
        int resultQuantity;
        int serial;

        if (material.Quantity == threshold)
        {
            var resultStack = material with
            {
                ItemId = resolved.ResultItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
            };
            working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, resultStack);
            resultItemId = resultStack.ItemId;
            resultQuantity = 0;
            serial = resultStack.Serial;
        }
        else
        {
            if (!TryFindEmptySlot(state, out grantedPage, out grantedIndex))
                return RejectedFamilyResult;

            var remainingMaterial = material with { Quantity = material.Quantity - threshold };
            working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, remainingMaterial);

            var newStack = new ItemStack(resolved.ResultItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                unchecked((int)DateTime.UtcNow.Ticks));
            grantedItem = newStack;

            if (grantedPage == packet.Page1)
            {
                working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem(grantedIndex, newStack);
            }
            else
            {
                EnsureContainer(working, state, grantedPage);
                working[grantedPage] = working[grantedPage].SetItem(grantedIndex, newStack);
            }

            resultItemId = remainingMaterial.ItemId;
            resultQuantity = remainingMaterial.Quantity;
            serial = remainingMaterial.Serial;
        }

        await PersistContainersAsync(characterId, working, cancellationToken);

        await eventLog.LogAsync(DustRecycleEventCode, EventLogCategory.ItemCreate, accountId, characterId,
            null, null, null, null, null, resolved.ResultItemId, 1, 1, null, cancellationToken);

        if (!await zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(characterId, SnapshotContainers(working), null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft (dust-recycle) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new CraftFamilyResult(CraftFamilyOutcome.Applied, resultItemId, resultQuantity, serial, grantedItem,
            grantedPage, grantedIndex);
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
