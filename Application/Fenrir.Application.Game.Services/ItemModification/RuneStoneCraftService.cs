using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for CZ_PROCESS_DATA_SEND tSort 3000 (rune-stone stat crafting) -- see
///     <see cref="IRuneStoneCraftService" /> for why this is not yet reachable from
///     <c>GenericActionHandler</c>'s dispatch switch.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:898-1127 (see <see cref="RuneStoneCraftResolver" /> for the
///     exact citation breakdown of the rule engine this orchestrates).
///     <para>
///         <b>Open integration question -- destination packed-stat storage:</b> Fenrir's
///         <c>ItemStack</c>/<c>CharacterItemSlotDto</c>/<c>Tvp</c> has no dedicated field yet for a "rune
///         core" item's packed STR/DEX/VIT/INT value. Server/Header/function.h:317-343 and :3348-3386
///         describe legacy accessor/mutator functions over some packed representation, but this pass has no
///         confirmed citation for which physical column backs it in the legacy schema -- it is plausible
///         (but NOT confirmed here, and must not be assumed) that the legacy item struct repurposes the same
///         4 upgrade bytes <c>ItemValueCodec</c> already models as Enchant/Combine/Refine/Socket for ordinary
///         equipment. Until <c>fenrir-database-engineer</c> adds a dedicated column (or
///         <c>cpp-zone-gameplay-analyst</c> confirms an existing one is reused), this service:
///         <list type="bullet">
///             <item>takes the destination's CURRENT packed stat as a plain caller-supplied <see cref="int" />, never read off <c>ItemStack</c>;</item>
///             <item>returns the NEW packed stat in its result for the caller to relay on the wire (<c>GenericActionResponse.RuneValue</c>);</item>
///             <item>does NOT persist the destination item's row itself, and does NOT mirror a destination-side change to the zone.</item>
///         </list>
///         The source item's consumption (quantity decrement / slot clear) has no such gap -- it only touches
///         <c>ItemStack.Quantity</c>, and IS fully persisted and mirrored below.
///     </para>
/// </remarks>
public sealed class RuneStoneCraftService(
    ICharacterRepository characters,
    IEventLogQueue eventLogQueue,
    ILogger<RuneStoneCraftService> logger)
    : IRuneStoneCraftService
{
    /// <summary>
    ///     game.EventLog.EventCode for a rune-stone-craft attempt -- the internal sub-opcode (tSort 3000)
    ///     itself, same "EventCode == the wire selector" convention <c>RuneSocketService</c> uses for its own
    ///     opcode-based codes. Shared by all 3 source items; the log payload's own Action= field (STATADD /
    ///     RANDOMSTAT / SLOTSTAT) is what distinguishes them, matching the legacy log text's own convention.
    /// </summary>
    private const short EventCode = 3000;

    public async ValueTask<RuneStoneCraftResult> CraftAsync(
        int sourcePage, int sourceSlot,
        int destinationPage, int destinationSlot,
        int statSlotSelector, int destinationPackedStat,
        bool secondInventoryPageAccessible,
        Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        // Bounds-checked before any byte cast, same posture as GenericActionService.MoveContainerAsync: an
        // out-of-range slot must never be truncated into a byte that could accidentally alias a real slot.
        var sourceStack = IsValidInventorySlot(sourcePage, sourceSlot)
            ? state.Inventory.GetSlot((byte)sourcePage, (byte)sourceSlot)
            : null;
        var destinationStack = IsValidInventorySlot(destinationPage, destinationSlot)
            ? state.Inventory.GetSlot((byte)destinationPage, (byte)destinationSlot)
            : null;

        var request = new RuneStoneCraftRequest(
            sourcePage, sourceSlot, sourceStack?.ItemId ?? 0,
            destinationPage, destinationSlot, destinationStack?.ItemId ?? 0, destinationPackedStat,
            statSlotSelector, secondInventoryPageAccessible);

        var resolved = RuneStoneCraftResolver.Resolve(request, SystemRandomSource.Instance);

        if (resolved.Outcome != RuneStoneCraftOutcome.Applied)
            return resolved;

        // Guaranteed non-null: Resolve only reaches Applied once request.SourceItemId matched the source
        // whitelist, which requires sourceStack to have been non-null in the first place (an empty slot
        // reports item id 0, which never matches).
        var source = sourceStack!.Value;
        var destinationItemId = destinationStack!.Value.ItemId;

        var consumed = RuneStoneCraftResolver.ConsumeOneUnit(source);
        var projectedContainer = consumed is { } remaining
            ? state.Inventory.GetContainer((byte)sourcePage).SetItem((byte)sourceSlot, remaining)
            : state.Inventory.GetContainer((byte)sourcePage).Remove((byte)sourceSlot);

        await characters.ReplaceContainerAsync(characterId, (byte)sourcePage, ToTvps(projectedContainer),
            cancellationToken);

        LogCraftAttempt(characterId, source.ItemId, destinationItemId, destinationPackedStat, resolved);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)sourcePage, projectedContainer));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped rune-stone-craft source mirror for character {CharacterId}",
                zone.MapId, characterId);

        return resolved;
    }

    private void LogCraftAttempt(int characterId, int sourceItemId, int destinationItemId, int packedStatBefore,
        RuneStoneCraftResult resolved)
    {
        var actionLabel = RuneStoneCraftCatalog.GetLogActionLabel(sourceItemId);
        var (beforeStr, beforeDex, beforeVit, beforeInt) = RuneStoneStatCodec.Decode(packedStatBefore);
        var (afterStr, afterDex, afterVit, afterInt) = RuneStoneStatCodec.Decode(resolved.NewPackedStat);

        var payload =
            $"Action={actionLabel};Source={sourceItemId};Destination={destinationItemId};Slot={resolved.LogSlotIndicator};" +
            $"Before=STR{beforeStr}/DEX{beforeDex}/VIT{beforeVit}/INT{beforeInt};" +
            $"After=STR{afterStr}/DEX{afterDex}/VIT{afterVit}/INT{afterInt}";

        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(EventCode, (byte)EventLogCategory.Enchant, null, characterId,
                null, null, null, null, null, sourceItemId, 1, (byte)resolved.ResultCode, payload,
                DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped rune-stone-craft audit row for character {CharacterId}",
                characterId);
    }

    private static bool IsValidInventorySlot(int page, int slot)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
