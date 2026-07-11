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
///     <para>
///         <b>C14 generalization.</b> The <see cref="Resolve" /> method above is the tSort-209 inventory-&gt;world
///         CALLER; <see cref="ReshapeGroundDrop" /> below is the shared spawn routine's own per-item-Sort
///         value/quantity reshape (<c>Server/ts25zone/S07_MyGame03.cpp:426-720</c>) that EVERY drop origin
///         funnels through, ported as a pure function so any producer (monster kill, treasure chest, cp-exchange,
///         level bonus, ...) can reuse it. <see cref="GroundDropOrigin" /> enumerates the 13 whitelisted origins
///         (<c>Server/Header/Protocol/DEFINE.h:517-529</c>); only 3 of their raw wire codes were recovered from
///         the C14 contract (<see cref="World.Loot.GroundItemEntity.MonsterKillDropSort" />=1,
///         <see cref="World.Loot.GroundItemEntity.ManualGroundDropSort" />=2,
///         <see cref="World.Loot.GroundItemEntity.GmCreateItemDropSort" />=13), so no raw-int "reject everything
///         not one of the 13" gate is built here -- it would wrongly reject the 10 origins whose wire codes are
///         unrecovered. The reshape needs only the monster/GM discriminators (both recovered), so it operates on
///         the raw <c>dropSort</c> int directly. <b>Unrecovered legacy constants, flagged in place:</b> the
///         item-<c>Type</c> gate's 6 accepted types (:464-475 -- left to the existing
///         <see cref="World.Loot.GroundItemSpawnEligibility" /> seam), equipment's improve/add band maxima
///         (:535-557), and the pat-activity max (:558-564).
///     </para>
/// </remarks>
public static class InventoryToWorldDropPolicy
{
    public enum GroundDropReshapeOutcome
    {
        Reshaped,
        RejectedUnhandledSort,
        RejectedQuantityRange,
        RejectedPackedValueRange
    }

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

    // ---- C14: shared MyUtil::ProcessForDropItem per-item-Sort value/quantity reshape ----
    // (whitelist + reshape, Server/ts25zone/S07_MyGame03.cpp:477-578). Pure; no I/O, no Zone dependency.

    /// <summary>
    ///     <c>MAX_NUMBER_SIZE</c> (<c>Server/Header/Protocol/DEFINE.h:365</c>) -- the money and pat-activity
    ///     quantity ceiling. Same value as <c>WorldState.TribeBankTaxAccumulator.ZoneLocalCeiling</c>, restated
    ///     here to avoid an Inventory-&gt;World.WorldState namespace dependency for one constant.
    /// </summary>
    public const long MaxNumberSentinel = 2_000_000_000L;

    /// <summary>
    ///     The 15% ground-money reduction a monster-&gt;world
    ///     (<see cref="World.Loot.GroundItemEntity.MonsterKillDropSort" />) currency drop takes before the
    ///     tribe-bank deposit and tower add-back -- <c>Server/ts25zone/S07_MyGame03.cpp:488-490</c>, gated
    ///     <c>#ifdef LNW33</c>, which is live in the shipped <c>ReleaseEU33</c> configuration
    ///     (<c>Server/ts25latest_config.props:9-12</c>).
    /// </summary>
    public const float MonsterMoneyGroundReductionRatio = 0.15f;

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

    /// <summary>
    ///     Ports the shared spawn routine's per-<c>Sort</c> value/quantity reshape -- the single point every drop
    ///     origin passes through after the drop-sort whitelist and item-type gates already passed. Returns the
    ///     reshaped quantity/value plus, for a monster-&gt;world money drop only, the tribe-bank deposit input
    ///     amount (the <c>AddTribeBankInfo3</c> caller applies the &gt;=100 floor + 9% itself).
    /// </summary>
    /// <param name="itemSort">The item's catalog <c>Sort</c> (<c>ItemRowDto.Sort</c>) -- selects the reshape rule.</param>
    /// <param name="dropSort">
    ///     The origin drop-sort code, compared only against the two recovered discriminators
    ///     (<see cref="World.Loot.GroundItemEntity.MonsterKillDropSort" /> for the money chain,
    ///     <see cref="World.Loot.GroundItemEntity.GmCreateItemDropSort" /> for the stackable/pat GM force-to-max).
    /// </param>
    /// <param name="ownerTowerSilverRatio">
    ///     The owning user's OWN tribe's current tower-silver ratio (0.05/0.10/0.15/0.20 by tower stage,
    ///     <c>Server/ts25zone/S07_MyGame01.cpp:13366-13369</c> -- <c>Progression.TowerTribeRewardBonus.SilverRatio</c>),
    ///     or 0 if that tribe holds no silver tower. Consulted ONLY for a monster-&gt;world money drop. Note the
    ///     contract's own edge case: this tribe is read from the owning user, a DIFFERENT source than the
    ///     <c>dropSort</c>-credited tribe the deposit uses.
    /// </param>
    public static GroundDropReshape ReshapeGroundDrop(byte itemSort, int dropSort, int incomingQuantity,
        int incomingValue, float ownerTowerSilverRatio = 0f)
    {
        switch (itemSort)
        {
            // Money (:484-508): value discarded; monster->world takes the 15% reduction + tribe-bank deposit +
            // tower add-back.
            case 1:
                return ReshapeMoney(dropSort, incomingQuantity, ownerTowerSilverRatio);

            // Stackable / material (:509-527). Sort 99 folds into the same handling under USE_MATS_999
            // (DEFINE.h:73, defined unconditionally).
            case 2 or 99:
            {
                var quantity = incomingQuantity;
                if (dropSort == GroundItemEntity.GmCreateItemDropSort && quantity == 0)
                    quantity = GroundItemPickupPolicy.MaxStackQuantity;
                else if (quantity == 0)
                    quantity = 1;

                if (quantity < 1 || quantity > GroundItemPickupPolicy.MaxStackQuantity)
                    return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedQuantityRange);

                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, quantity, 0, 0);
            }

            // Pets / mounts / wings (:528-534): no stack count, no packed value.
            case 3 or 4 or 5 or 6:
                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, 0, 0, 0);

            // Equipment (:535-557): quantity forced to zero; the packed value is carried onto the object
            // UNCHANGED. NOTE (unrecovered): the legacy improve(0..max-improve)/add(0..max-add) band check on the
            // decoded sub-values is not reproduced -- the C14 contract confirms the check exists but supplies
            // neither maximum, and re-validating with invented bounds could only add a wrong reject path.
            // Flagged for cpp-zone-gameplay-analyst.
            case >= 7 and <= 21:
                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, 0, incomingValue, 0);

            // Companion / "pat" activity (:558-564).
            case 22:
            {
                if (incomingValue < 0 || incomingValue > MaxNumberSentinel)
                    return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedPackedValueRange);

                // NOTE (unrecovered): the GM->world "force quantity to max-pat-activity" substitution and the
                // 0..max-pat-activity range check both need MAX_PAT_ACTIVITY, which the C14 contract does not
                // supply -- so the quantity passes through unchanged and is NOT clamped/validated. Flagged.
                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, incomingQuantity, incomingValue, 0);
            }

            // Pass-through, no reshape (:565-576).
            case >= 23 and <= 33:
                return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, incomingQuantity, incomingValue, 0);

            // Any other sort: rejected (:577-578).
            default:
                return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedUnhandledSort);
        }
    }

    private static GroundDropReshape ReshapeMoney(int dropSort, int incomingQuantity, float ownerTowerSilverRatio)
    {
        long ground = incomingQuantity;
        long deposit = 0;

        if (dropSort == GroundItemEntity.MonsterKillDropSort)
        {
            ground -= (long)(ground * MonsterMoneyGroundReductionRatio);
            if (ground > 0)
                // Deposit input = the post-15% amount, BEFORE the tower add-back (side effect 4-money ordering).
                // AddTribeBankInfo3 applies the >=100 floor + 9% itself (S07_MyGame01.cpp:3076-3080), which
                // Fenrir models in TribeBankTaxAccumulator.CreditMonsterKillCurrencyTax -- so the raw reduced
                // amount is what a caller passes to Zone.CreditMonsterKillTribeTax.
                deposit = ground;

            // Tower add-back: increases the GROUND amount by the owning tribe's own tower-silver ratio, leaving
            // the already-captured deposit untouched (S07_MyGame03.cpp:496-501).
            ground += (long)(ground * ownerTowerSilverRatio);
        }

        if (ground < 1 || ground > MaxNumberSentinel)
            return GroundDropReshape.Reject(GroundDropReshapeOutcome.RejectedQuantityRange);

        return new GroundDropReshape(GroundDropReshapeOutcome.Reshaped, (int)ground, 0, deposit);
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

    /// <summary>
    ///     One reshaped ground-drop plan. <see cref="TribeBankDepositAmount" /> is non-zero only for a
    ///     monster-&gt;world money drop -- the post-15%-reduction amount to hand to
    ///     <c>Zone.CreditMonsterKillTribeTax</c>; zero for every other sort/origin.
    /// </summary>
    public readonly record struct GroundDropReshape(
        GroundDropReshapeOutcome Outcome,
        int Quantity,
        int Value,
        long TribeBankDepositAmount)
    {
        public bool Reshaped => Outcome == GroundDropReshapeOutcome.Reshaped;

        internal static GroundDropReshape Reject(GroundDropReshapeOutcome outcome)
        {
            return new GroundDropReshape(outcome, 0, 0, 0);
        }
    }
}

/// <summary>
///     The 13 whitelisted drop-sort origins <c>MyUtil::ProcessForDropItem</c> accepts
///     (<c>Server/ts25zone/S07_MyGame03.cpp:438-456</c>, semantics from
///     <c>Server/Header/Protocol/DEFINE.h:517-529</c>). Only <see cref="MonsterToWorld" /> (wire code 1),
///     <see cref="InventoryToWorld" /> (2), and <see cref="GmToWorld" /> (13) have a recovered raw wire code
///     (see <see cref="World.Loot.GroundItemEntity" />'s own constants); the other ten's wire codes were NOT
///     supplied by the C14 contract, so this enum is a semantic classifier for the reshape/notice logic, not a
///     raw-int-decoding table. Used by <see cref="EliteDropNoticeResolver" /> to pick a per-origin notice type
///     without needing those unrecovered wire codes.
/// </summary>
public enum GroundDropOrigin
{
    MonsterToWorld,
    PvpToWorld,
    InventoryToWorld,
    GmToWorld,
    BuyTribeItemToWorld,
    Zone175ToWorld,
    Zone194ToWorld,
    Zone200ToWorld,
    CpExchangeToWorld,
    LevelBonusToWorld,
    TreasureChestToWorld,
    TreasureChest175ToWorld,
    InventoryCrossTransferToWorld
}

/// <summary>
///     The <c>ELITE_NOTICE</c> payload (<c>Server/Header/Protocol/STRUCT.h:1306-1313</c>): a per-drop-sort type
///     code, the owning user's tribe, the item index (carried in the value field), and the owning user's name.
///     The struct's own <c>box</c> field is left unset (0), matching the source.
/// </summary>
public readonly record struct EliteDropNotice(int Type, byte Tribe, int Value, string AvatarName)
{
    /// <summary>Treasure-chest drop-sort type code.</summary>
    public const int TypeTreasureChest = 55;

    /// <summary>Treasure-chest-175 drop-sort type code.</summary>
    public const int TypeTreasureChest175 = 56;

    /// <summary>Monster-&gt;world drop-sort type code.</summary>
    public const int TypeMonster = 0;

    /// <summary>CP-exchange-&gt;world drop-sort type code.</summary>
    public const int TypeCpExchange = 1;

    /// <summary>PvP-&gt;world drop-sort type code.</summary>
    public const int TypePvp = 2;
}

/// <summary>
///     Pure resolver for <c>MyUtil::ProcessForDropItem</c>'s "elite/notable drop" notice, the cluster-wide
///     center relay under broadcast command 2000 (<c>Server/ts25zone/S07_MyGame03.cpp:650-717</c>). Returns the
///     <see cref="EliteDropNotice" /> to relay, or <see langword="null" /> when the drop does not announce.
/// </summary>
/// <remarks>
///     Only five origins qualify (treasure-chest, treasure-chest-175, monster-&gt;world, cp-exchange-&gt;world,
///     pvp-&gt;world); every other origin yields the unset type sentinel and no notice. The "show name" gate is
///     ported faithfully, including its two dead-in-production facts: ordinary elite-tier items never announce
///     (the auto-announce block is commented out in the source), so a <see cref="GroundDropOrigin.MonsterToWorld" />
///     or <see cref="GroundDropOrigin.CpExchangeToWorld" /> drop of an ordinary item returns
///     <see langword="null" /> here. <b>Unrecovered:</b> the eight ground-mountable animal item indices that are
///     "never announced (test forced off)" were not supplied by the C14 contract, so that one suppression cannot
///     be reproduced -- its only live effect is on a pvp-&gt;world animal drop (which the pvp-forced branch would
///     otherwise announce); flagged for cpp-zone-gameplay-analyst.
/// </remarks>
public static class EliteDropNoticeResolver
{
    public static EliteDropNotice? Resolve(GroundDropOrigin origin, int itemId, byte ownerTribe, string ownerName)
    {
        var type = origin switch
        {
            GroundDropOrigin.TreasureChestToWorld => EliteDropNotice.TypeTreasureChest,
            GroundDropOrigin.TreasureChest175ToWorld => EliteDropNotice.TypeTreasureChest175,
            GroundDropOrigin.MonsterToWorld => EliteDropNotice.TypeMonster,
            GroundDropOrigin.CpExchangeToWorld => EliteDropNotice.TypeCpExchange,
            GroundDropOrigin.PvpToWorld => EliteDropNotice.TypePvp,
            _ => (int?)null
        };

        if (type is not { } typeCode || !ShouldShowName(origin, itemId))
            return null;

        return new EliteDropNotice(typeCode, ownerTribe, itemId, ownerName);
    }

    private static bool ShouldShowName(GroundDropOrigin origin, int itemId)
    {
        // pvp->world: forced to announce regardless of item ("regardless of item" is taken to override the
        // 1002-1005 treasure-chest-only rule below). The 8-animal "never announce" suppression that could
        // override even this is unrecovered -- see class remarks.
        if (origin == GroundDropOrigin.PvpToWorld)
            return true;

        // Pets 1002-1005: announced only for the two treasure-chest origins.
        if (itemId is >= 1002 and <= 1005)
            return origin is GroundDropOrigin.TreasureChestToWorld or GroundDropOrigin.TreasureChest175ToWorld;

        // Default: the elite-tier auto-announce block is commented out in the source, so nothing else announces.
        return false;
    }
}
