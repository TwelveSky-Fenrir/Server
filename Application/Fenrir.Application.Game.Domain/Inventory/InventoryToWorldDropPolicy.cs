using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for <c>CZ_PROCESS_DATA_SEND</c>'s tSort 209 -- "drop an item from
///     inventory onto the ground at the player's own current position." The mirror image of
///     <see cref="GroundItemPickupPolicy" /> (world -&gt; inventory); this is inventory -&gt; world. Reuses
///     <see cref="ContainerMatrix" /> for the two-page/64-slot inventory bounds and
///     <see cref="GroundItemPickupPolicy.MaxStackQuantity" /> for the shared 999 stack cap.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:1129-1233 (<c>ProcessForInventoryToWorld</c> -- bounds
///     checks, premium-page gate, item resolution, non-droppable-item gate, the stackable-vs-unique branch,
///     the call into the shared ground-item-spawn routine, and the outcome-code convention) ; :72-82
///     (<c>ClearInventory</c> -- exact fields zeroed on a full slot clear) ; :171-184 (<c>INVENTORY_WORK</c>) ;
///     Server/ts25zone/S04_MyWork04.cpp:441-505 (dispatch table: tSort 209 invokes this function) ; :303-306,
///     2112-2122 (outer <c>ProcessForData</c> epilogue -- the malformed-disconnect / soft-failure /
///     still-ready-at-completion convention this type's <see cref="Outcome" /> values are shaped to map onto
///     1:1) ; Server/Header/Protocol/DEFINE.h:287-288 (2 inventory pages, 64 slots each) ; :611
///     (<c>MAX_ITEM_DUPLICATION_NUM</c> = 999, reused here via <see cref="GroundItemPickupPolicy.MaxStackQuantity" />) ;
///     :519 (this operation's own drop-origin tag, ported as <see cref="GroundItemEntity.ManualGroundDropSort" />) ;
///     Server/Header/Protocol/STRUCT.h:65 (declares the non-droppable-item catalog flag, Fenrir's
///     <c>ItemRowDto.CheckAvatarDrop</c>) ; Server/ts25zone/S07_MyGame03.cpp:426-720
///     (<c>MyUtil::ProcessForDropItem</c>, the shared spawn routine -- item-type gate, packed-value
///     validation, the zero-requested-quantity-&gt;one substitution for stackable items, and the 4000-entry
///     capacity gate) ; Server/ts25zone/H01_MainApplication.h:134 (the 4000 capacity constant itself,
///     <see cref="GroundItemCapacity" />) ; ServerDocs/12_ts25zone/07_MyWork05_Helpers.md:124-172,180-231.
///     <para>
///         <b>Two deliberately unresolved handoffs, flagged here rather than guessed at:</b>
///     </para>
///     <para>
///         1) <paramref name="sourceIsDroppableByPlayer" /> is a caller-supplied bool, not a hardcoded
///         <c>ItemRowDto.CheckAvatarDrop</c> comparison performed inside this policy. The behavior contract
///         this policy implements confirms a "disallow" value exists for that flag but the cited range does
///         not pin down its exact number, and this codebase's sibling field <c>CheckAvatarTrade</c> is
///         checked as <c>== 1</c> elsewhere (<see cref="TradeItemPlacementResolver.ResolveDeposit" />)
///         -- a plausible, but NOT independently re-confirmed for <c>CheckAvatarDrop</c> specifically, analogy.
///         Whoever wires the real caller should compute this bool explicitly (citing whichever convention it
///         settles on) rather than this policy silently assuming one.
///     </para>
///     <para>
///         2) The shared spawn routine's own item-type gate and packed-per-category-value validation
///         (<see cref="GroundItemSpawnEligibility" />) are injected via <paramref name="evaluateSpawnEligibility" />
///         rather than hardcoded -- the cited ~300-line routine's exact type whitelist and per-category value
///         ranges were not re-derived in this pass, and Fenrir's own <see cref="Zone.SpawnGroundItem" />
///         enforces neither gate today (monster loot bypasses both entirely -- see that method's own
///         "Tick-owned caller only" remarks, and its hardcoded Value=0/SerialNumber=0/no-socket-data spawn
///         shape, which does NOT yet support this operation's unique-item socket carry-over either). A
///         permissive <c>static _ =&gt; GroundItemSpawnEligibility.Eligible</c> predicate reproduces today's
///         Fenrir posture (no gate at all) until a dedicated contract for <c>ProcessForDropItem</c>'s
///         category tables exists, and until <see cref="Zone" /> grows a capacity/value/socket-aware
///         spawn entry point a caller can safely reach off the tick thread.
///     </para>
///     <para>
///         Per the same contract: the "notable item" server-wide announcement the shared spawn routine can
///         raise for other drop origins (monster kill, PvP, treasure chest/catapult) is explicitly excluded
///         for this operation's own drop-origin tag -- nothing in this policy ever signals it, and no caller
///         should wire it in for tSort 209.
///     </para>
/// </remarks>
public static class InventoryToWorldDropPolicy
{
    public enum Outcome
    {
        // Malformed / hostile input -- the caller must disconnect the session; no acknowledgment is ever sent.
        SourceOutOfRange,
        PremiumPageExpired,
        UnknownItem,
        NonDroppableItem,
        QuantityOutOfRange,
        InsufficientQuantity,

        // Soft failure -- the caller replies with the standard acknowledgment carrying a generic non-zero
        // code; the source slot is left completely untouched; the session is NOT disconnected.
        UnsupportedItemType,
        InvalidPackedValue,
        GroundItemTableFull,

        Success
    }

    /// <summary>Server/ts25zone/H01_MainApplication.h:134 -- shared across every drop origin, not just this one.</summary>
    public const int GroundItemCapacity = 4000;

    /// <param name="sourcePage">
    ///     tPage1 -- valid range 0-1 (<see cref="ContainerMatrix.InventoryPage0" />/
    ///     <see cref="ContainerMatrix.InventoryPage1" />).
    /// </param>
    /// <param name="sourceSlot">tIndex1 -- valid range 0-63.</param>
    /// <param name="requestedQuantity">
    ///     tQuantity1 -- only consulted when the resolved item is stackable; ignored entirely for a unique item
    ///     (the whole slot always drops regardless of this value in that case).
    /// </param>
    /// <param name="premiumPageAccessAllowed">
    ///     Only consulted when <paramref name="sourcePage" /> is the second/premium page -- see this type's own
    ///     remarks on why this is caller-supplied (no such expiry field exists on <c>PlayerRuntimeState</c> yet).
    /// </param>
    /// <param name="source">The source slot's current contents, or <see langword="null" /> if genuinely empty.</param>
    /// <param name="itemDefinition">
    ///     The source item's catalog row, or <see langword="null" /> if <paramref name="source" />'s item id does
    ///     not resolve to any known item -- both collapse to <see cref="Outcome.UnknownItem" />, same posture as
    ///     every sibling container-move policy in this codebase (e.g. <see cref="StoreItemTransferPolicy" />).
    /// </param>
    /// <param name="sourceIsDroppableByPlayer">See this type's own remarks, handoff 1.</param>
    /// <param name="evaluateSpawnEligibility">See this type's own remarks, handoff 2.</param>
    /// <param name="currentGroundItemCount">The dropping player's zone's current live ground-item count.</param>
    /// <param name="dropperName">The dropping character's own current name -- used as ground-item owner when unpartied.</param>
    /// <param name="dropperPartyName">
    ///     The dropping character's current party identity (<see cref="PartyIdentityResolver.ResolveCurrentPartyName" />),
    ///     or <see langword="null" />/empty if unpartied -- used as ground-item owner instead of
    ///     <paramref name="dropperName" /> when non-empty.
    /// </param>
    public static Result Resolve(
        int sourcePage,
        int sourceSlot,
        int requestedQuantity,
        bool premiumPageAccessAllowed,
        ItemStack? source,
        ItemDefinition? itemDefinition,
        bool sourceIsDroppableByPlayer,
        Func<ItemDefinition, GroundItemSpawnEligibility> evaluateSpawnEligibility,
        int currentGroundItemCount,
        float dropperPosX, float dropperPosY, float dropperPosZ,
        string dropperName, string? dropperPartyName)
    {
        if (sourcePage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)sourcePage, sourceSlot))
            return Fail(Outcome.SourceOutOfRange);

        if (sourcePage == ContainerMatrix.InventoryPage1 && !premiumPageAccessAllowed)
            return Fail(Outcome.PremiumPageExpired);

        if (source is not { } src)
            return Fail(Outcome.UnknownItem);

        if (itemDefinition is null)
            return Fail(Outcome.UnknownItem);

        if (!sourceIsDroppableByPlayer)
            return Fail(Outcome.NonDroppableItem);

        var isStackable = ContainerMatrix.IsStackableSort(itemDefinition.Item.Sort);

        // The requested quantity is meaningful, and validated, only for the stackable path -- a unique item
        // never partially drops and never rejects on this field (contract's own explicit "never consulted").
        if (isStackable)
        {
            if (requestedQuantity < 0 || requestedQuantity > GroundItemPickupPolicy.MaxStackQuantity)
                return Fail(Outcome.QuantityOutOfRange);

            if (requestedQuantity > src.Quantity)
                return Fail(Outcome.InsufficientQuantity);
        }

        var eligibility = evaluateSpawnEligibility(itemDefinition);
        if (eligibility == GroundItemSpawnEligibility.UnsupportedItemType)
            return Fail(Outcome.UnsupportedItemType);
        if (eligibility == GroundItemSpawnEligibility.InvalidPackedValue)
            return Fail(Outcome.InvalidPackedValue);

        if (currentGroundItemCount >= GroundItemCapacity)
            return Fail(Outcome.GroundItemTableFull);

        var owner = string.IsNullOrEmpty(dropperPartyName) ? dropperName : dropperPartyName;

        if (isStackable)
        {
            // Zero-requested-quantity duplication quirk (faithfully reproduced, not "fixed" -- see this
            // policy's own Resolve doc / the behavior contract's Edge cases): the shared spawn routine always
            // substitutes a ground quantity of 1 when asked for 0, while the source stack is reduced by the
            // RAW requested amount (0 => untouched). A repeated 0-quantity drop is therefore a real,
            // repeatable, cost-free duplication of 1 unit at a time, bounded only by the ground-item capacity.
            var groundQuantity = requestedQuantity == 0 ? 1 : requestedQuantity;
            var spawn = new GroundItemSpawnPlan(itemDefinition.Item.ItemId, groundQuantity, 0, 0, 0, 0, 0,
                dropperPosX, dropperPosY, dropperPosZ, owner, GroundItemEntity.ManualGroundDropSort);

            var remaining = src.Quantity - requestedQuantity;
            var newSource = remaining > 0 ? src with { Quantity = remaining } : (ItemStack?)null;
            return new Result(Outcome.Success, newSource, spawn);
        }

        // Unique item: the whole slot drops, unconditionally cleared (ClearInventory's full reset); its
        // packed enchant/combine/refine/socket value and gem-socket contents carry over to the ground copy.
        var value = ItemValueCodec.Encode(src.Enchant, src.Combine, src.Refine, src.Socket);
        var uniqueSpawn = new GroundItemSpawnPlan(itemDefinition.Item.ItemId, src.Quantity, value, src.Serial,
            src.SocketGem1, src.SocketGem2, src.SocketGem3, dropperPosX, dropperPosY, dropperPosZ, owner,
            GroundItemEntity.ManualGroundDropSort);

        return new Result(Outcome.Success, null, uniqueSpawn);
    }

    private static Result Fail(Outcome outcome)
    {
        return new Result(outcome, null, null);
    }

    public readonly record struct Result(Outcome Outcome, ItemStack? NewSource, GroundItemSpawnPlan? Spawn)
    {
        /// <summary>Disconnect the session; no acknowledgment is ever sent for this request.</summary>
        public bool IsMalformed => Outcome is Outcome.SourceOutOfRange or Outcome.PremiumPageExpired
            or Outcome.UnknownItem or Outcome.NonDroppableItem or Outcome.QuantityOutOfRange
            or Outcome.InsufficientQuantity;

        /// <summary>Reply with the standard non-zero-code acknowledgment; source left untouched; no disconnect.</summary>
        public bool IsSoftFailure => Outcome is Outcome.UnsupportedItemType or Outcome.InvalidPackedValue
            or Outcome.GroundItemTableFull;

        public bool Succeeded => Outcome == Outcome.Success;
    }
}
