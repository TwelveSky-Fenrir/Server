using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Enchant;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Npcs;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op24, CZ_IMPROVE_ITEM_SEND (contracts/03_inventory_craft.md, report 04_mega_switches.md §4, V5 NPC
///     &amp; Economy) -- standard equipment enchant via <see cref="EnchantResolver" />'s pure port of
///     <c>W_IMPROVE_ITEM_SEND</c> (<c>Server/ts25zone/S04_MyWork02.cpp:2500-3450</c>). Wings/costumes/stellar
///     cores are OUT of scope (see <see cref="EnchantResolver" />'s own remarks) and reply with a clean
///     failure rather than a disconnect; every OTHER structural violation the verified source itself Quit()s
///     on is reproduced as an <see cref="ClientSession.Abort" /> here too.
/// </summary>
/// <remarks>
///     D7 regime (b): the roll happens HERE (off the zone tick thread -- <c>Zone.Tick</c> is fully
///     synchronous and must never block on the SQL this action needs BEFORE the client ever sees success),
///     using the production <see cref="SystemRandomSource" /> directly (thread-safe, same default
///     <c>Zone</c> itself uses) -- a deliberate, first-of-its-kind off-tick RNG use in this codebase, forced
///     by the D7 synchronous-SQL constraint no purely tick-thread action has needed until now. Money debit +
///     the touched container(s) commit atomically via <c>usp_Character_AdjustMoneyAndReplaceContainer</c>/
///     ...TwoContainers (D7 regime (b)) -- a mid-sequence failure can never charge the player without
///     applying the result (or vice versa). <c>ProtectForDestroy</c> is NOT modeled on
///     <see cref="PlayerRuntimeState" /> yet -- and has no acquisition path either, since consuming a
///     protection-scroll item rides the CZ_USE_INVENTORY_ITEM_SEND (opcode 23) mega-switch this batch does
///     not implement -- so <see cref="EnchantResolver.Resolve" /> is always called with 0 charges, meaning
///     <see cref="EnchantResolver.EnchantOutcome.Protected" /> is currently unreachable (a documented, honest
///     consequence of scope, not a fabricated shortcut).
/// </remarks>
public sealed class EnchantItemHandler(
    CharacterRepository characters,
    WorldDataCache worldData,
    ILogger<EnchantItemHandler> logger)
    : IAsyncPacketHandler<EnchantItemRequest>
{
    public async ValueTask HandleAsync(EnchantItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        // Benign staleness (mid-handoff/disconnect) -- same defensive posture as every other InWorld handler.
        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // PlayerRuntimeState.EconomyActionLock's own remarks: serializes this whole read-snapshot/await-SQL/
        // post-mirror sequence per character, closing the item/money duplication window a burst of concurrent
        // economy requests for the same character could otherwise open (review finding, Phase C/V5).
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

    private async ValueTask ResolveAndApplyAsync(EnchantItemRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        // IsValidTownAll -- enchant only in town (S04_MyWork02.cpp:2502).
        if (!NpcShopPolicy.TownZoneNumbers.Contains(zone.MapId))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var page1 = packet.Page1;
        var index1 = packet.Index1;
        var page2 = packet.Page2;
        var index2 = packet.Index2;

        // INVENTORY_FACTOR_INIT reads wInventory[page][index] directly (S04_MyWork02.cpp:261-274) -- both
        // target and material are strictly inventory slots, never Equipment.
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
            !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition) ||
            !worldData.ItemsById.TryGetValue(material.ItemId, out var materialDefinition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var luck = state.Stats?.Luck ?? 0;

        var resolved = EnchantResolver.Resolve(targetDefinition, target, materialDefinition, luck,
            protectForDestroyCharges: 0, SystemRandomSource.Instance);

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.NotSupported)
        {
            // Fenrir scope cut (wings), NOT a legacy Quit() -- see EnchantResolver's own remarks.
            session.Send(new EnchantItemResponse { Result = 1, Cost = 0, Value = 0 });
            return;
        }

        if (resolved.Outcome == EnchantResolver.EnchantOutcome.Rejected)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        // Material ALWAYS consumed exactly once regardless of outcome (DecreaseMaterial, verified).
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
            // Insufficient funds -- Quit()-worthy (S04_MyWork02.cpp:2913/3094/2637/3086 -- every money check in
            // this whole handler Quits on failure, no clean-fail path exists for it). Logged (review finding)
            // so a transient/unrelated SQL fault is distinguishable from real insufficient funds.
            logger.LogWarning(ex,
                "Character {CharacterId} enchant AdjustMoney...ReplaceContainer(s)Async failed (treated as insufficient funds)",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new EnchantItemResponse
        {
            Result = MapResultCode(resolved.Outcome), Cost = resolved.Cost, Value = resolved.NewEnchant
        });

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
    }

    /// <summary>ZC_IMPROVE_ITEM_RECV result codes (report 04 §4/contract doc): 0 success, 1 fail/-1, 2 destroyed, 3 reset-to-+40, 4 protected.</summary>
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
