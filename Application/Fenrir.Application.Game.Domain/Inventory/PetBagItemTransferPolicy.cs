using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for the pet/animal cargo-bag item-move family (<c>CZ_PROCESS_DATA_SEND</c>
///     tSort 254 deposit from general inventory, 255 withdraw to general inventory, 256 bag-to-bag rearrange).
///     Unlike <see cref="StoreItemTransferPolicy" />/<see cref="SaveBankItemTransferPolicy" />, the pet bag's own
///     20 slots (<see cref="BagSlotCount" />) never carry an <see cref="ItemStack" /> -- the legacy bag array is a
///     bare catalog-id-per-slot store with no quantity/value/serial/socket/expiration fields at all, so a bag slot
///     is modeled here as a plain nullable <see cref="int" /> (catalog item id; <see langword="null" /> covers both
///     "slot truly empty" and "stored id fails catalog lookup", the same collapse convention the sibling policies
///     already use for <see cref="ItemStack" />-based slots).
///     <para>
///         All three directions are hard-fail-only: every rejection in every direction ends the session
///         (disconnect), there is no soft in-band "denied" response anywhere in these three legacy functions --
///         unlike some sibling tSorts in the same family. Callers should treat every <see cref="TransferOutcome" />
///         other than <see cref="TransferOutcome.Success" />/<see cref="TransferOutcome.NoOp" /> as
///         disconnect-worthy, not as a response to send back in-band.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3806-3878 (Direction A, deposit) ; :3880-3970 (Direction B,
///     withdraw) ; :3972-4041 (Direction C, rearrange) ; :239-246 (<c>CheckMinMax</c>, the shared bounds-check
///     helper -- valid range is value &gt;= min and value &lt; max) ; :72-82 (<c>ClearInventory</c>, the full
///     general-inventory-slot reset Direction A's source undergoes) ; :171-184 (the general-inventory slot's field
///     layout: index, x, y, quantity, value, serial -- "packed value field" here means
///     <c>Enchant</c>/<c>Combine</c>/<c>Refine</c>/<c>Socket</c> collectively, decoded already on
///     <see cref="ItemStack" /> per <see cref="Fenrir.Application.Game.Domain.World.Loot.ItemValueCodec" />) ;
///     Server/Header/Protocol/STRUCT.h:520-521 (<c>aPetBagDate</c>/the 20-slot pet-bag array, bare catalog index per
///     slot) ; STRUCT.h:1671 (the pet equipment slot's enum value -- <see cref="PetSlots.EquipmentSlot" />) ;
///     STRUCT.h:1229-1238 (the shared 7-field generic wire payload, not reproduced here -- wire remapping is a
///     dispatcher/handler concern, out of scope for this pure policy) ; Server/Header/Protocol/DEFINE.h:287-288
///     (general-inventory page count/slots, reused via <see cref="ContainerMatrix" />) ; DEFINE.h:393 (pet-bag slot
///     count = 20) ; DEFINE.h:611/:73 (the stackable-duplication cap and its governing macro, both referenced only
///     by Direction B's merge branch, which is dead code here -- pet-bag items are never stackable-category, so
///     Direction B's "destination occupied" is an unconditional rejection, no merge path modeled) ;
///     Server/Header/Protocol/CLIENT.h:526-527 (opcode 19, the generic container-action packet -- dispatch-layer
///     concern) ; Server/ts25zone/S04_MyWork02.cpp:1990-2014 and S04_MyWork04.cpp:441-482,774-803,2112-2122 (the
///     bridge/dispatch/epilogue machinery around these three functions -- entirely out of scope for this pure
///     policy, which models only the 3 function bodies' own decisions and state changes) ;
///     Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:945-949 and H06_MyUpperCom.h:349 (the audit-log call's exact
///     content/shape -- Fenrir has no durable audit-log subsystem yet, so <see cref="DepositResult.ShouldEmitAuditLog" />
///     / <see cref="WithdrawResult.ShouldEmitAuditLog" /> only signal *that* a call is owed once one exists; the
///     caller already holds every value that call needs -- character identity, the transferred catalog id, and
///     either the pre-transfer <see cref="ItemStack.Serial" /> (Direction A) or the freshly generated serial it
///     passed in (Direction B) -- so no extra output fields are needed here) ; Server/Header/function.h:5-26 (the
///     new-serial-number generator's rarity-tier gate/time-derived formula that Direction B's destination slot uses
///     -- not modeled here since Fenrir has no equivalent catalog rarity-tier data yet; exposed as the
///     caller-supplied <c>newSerialNumber</c> parameter, expected to be 0 for genuine pet-category items) ;
///     ServerDocs/12_ts25zone/11_MyGame03_PartieA.md:317-322 (catalog sort codes 3-6 form a "mounts/companions/wings"
///     family elsewhere in the legacy system; only code 3, <see cref="PetBagEligibleSort" />, is ever accepted into
///     the pet bag by Direction A/C) ; ServerDocs/12_ts25zone/07_MyWork05_Helpers.md:385-409 (§9, corroborating the
///     disabled pet-validity check -- only the raw "&gt;= 1" floor test on the pet equipment slot actually runs).
///     <para>
///         The two expiry-dated entitlement gates (pet-bag slots 10-19, and the general-inventory second page) are
///         exposed as caller-supplied bools -- the same posture <see cref="StoreItemTransferPolicy" /> and
///         <see cref="SaveBankItemTransferPolicy" /> already use for their own second-page gates: neither
///         <c>PlayerRuntimeState</c> nor <c>Fenrir.Data.Abstractions</c> carries either date field yet. Same posture
///         for <c>petEquipped</c>: the legacy check is a raw "stored id &gt;= 1" floor test with the real
///         pet-validity check commented out (dead code), so the caller need only test
///         <see cref="PetSlots.ResolveEquippedPetItemId" />'s result against that same floor, never a catalog
///         lookup.
///     </para>
/// </remarks>
public static class PetBagItemTransferPolicy
{
    public enum TransferOutcome
    {
        Success,

        /// <summary>
        ///     Direction C only: same bag slot named as both source and destination -- bypasses every other
        ///     guard (pet-equipped, both entitlement gates, catalog lookups) entirely, per the legacy special case.
        /// </summary>
        NoOp,

        SourceOutOfRange,
        DestinationOutOfRange,

        /// <summary>Direction B only: destinationX/destinationY must each be 0-7.</summary>
        DestinationCoordinateOutOfRange,

        /// <summary>Pet equipment slot's stored catalog id is below 1 -- checked before any container-content check.</summary>
        PetNotEquipped,

        /// <summary>A touched bag slot (either side) is 10-19 and the upper-half entitlement is not currently active.</summary>
        PetBagUpperHalfExpired,

        /// <summary>The touched general-inventory side is page 1 and its own second-page entitlement is not active.</summary>
        SecondInventoryPageExpired,

        /// <summary>Source slot is empty, or (bag side) its stored id fails catalog lookup.</summary>
        SourceEmpty,

        /// <summary>
        ///     Direction A only: source's quantity is above zero, or its packed value field (Enchant/Combine/
        ///     Refine/Socket) is non-zero -- checked before the source's catalog sort code is even looked at.
        /// </summary>
        SourceNotAtRest,

        /// <summary>
        ///     Direction A/C only: source's catalog sort code is not <see cref="PetBagEligibleSort" />. Direction
        ///     B never performs this check -- it unconditionally trusts the bag slot's stored id.
        /// </summary>
        SourceItemNotPetEligible,

        /// <summary>No merge, no overwrite, no swap exists for any of these three transfers.</summary>
        DestinationOccupied
    }

    /// <summary>MAX_PET_BAG_SLOT_NUM-equivalent (DEFINE.h:393).</summary>
    public const int BagSlotCount = 20;

    public const int MaxBagSlotInclusive = BagSlotCount - 1;

    /// <summary>Bag slots &gt;= this need the <c>aPetBagDate</c>-equivalent entitlement; slots 0-9 need nothing.</summary>
    public const int UpperHalfStartSlot = 10;

    /// <summary>
    ///     The one catalog sort code, out of the broader 3-6 "mounts/companions/wings" family, ever accepted into the
    ///     pet bag by Direction A/C (ServerDocs/12_ts25zone/11_MyGame03_PartieA.md:317-322).
    /// </summary>
    public const byte PetBagEligibleSort = 3;

    public static bool IsValidBagSlot(int slot)
    {
        return slot is >= 0 and <= MaxBagSlotInclusive;
    }

    public static bool RequiresUpperHalfEntitlement(int bagSlot)
    {
        return bagSlot >= UpperHalfStartSlot;
    }

    /// <summary>tSort 254 -- general-inventory slot to pet-bag slot.</summary>
    public static DepositResult ResolveDepositFromInventory(
        byte inventoryContainer, int inventorySlot, int petBagSlot,
        ItemStack? source, int? destinationBagItemId, byte sourceItemSort,
        bool petEquipped, bool bagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive)
    {
        if (!ContainerMatrix.IsValidSlot(inventoryContainer, inventorySlot))
            return DepositFail(TransferOutcome.SourceOutOfRange);

        if (!IsValidBagSlot(petBagSlot))
            return DepositFail(TransferOutcome.DestinationOutOfRange);

        if (!petEquipped)
            return DepositFail(TransferOutcome.PetNotEquipped);

        if (RequiresUpperHalfEntitlement(petBagSlot) && !bagUpperHalfEntitlementActive)
            return DepositFail(TransferOutcome.PetBagUpperHalfExpired);

        if (inventoryContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageEntitlementActive)
            return DepositFail(TransferOutcome.SecondInventoryPageExpired);

        if (source is not { } src)
            return DepositFail(TransferOutcome.SourceEmpty);

        if (src.Quantity > 0 || src.Enchant != 0 || src.Combine != 0 || src.Refine != 0 || src.Socket != 0)
            return DepositFail(TransferOutcome.SourceNotAtRest);

        if (sourceItemSort != PetBagEligibleSort)
            return DepositFail(TransferOutcome.SourceItemNotPetEligible);

        if (destinationBagItemId is not null)
            return DepositFail(TransferOutcome.DestinationOccupied);

        return new DepositResult(TransferOutcome.Success, null, src.ItemId, true);
    }

    /// <summary>
    ///     tSort 255 -- pet-bag slot to general-inventory slot. Deliberately performs no catalog-sort-code
    ///     eligibility check on the withdrawn item (see <see cref="TransferOutcome.SourceItemNotPetEligible" />'s
    ///     remarks) -- it trusts whatever <paramref name="sourceBagItemId" /> the caller resolved. destinationX/Y
    ///     are range-validated only; like the sibling Store/Bank withdraw policies, Fenrir has no field to persist
    ///     visual grid placement on <see cref="ItemStack" /> yet, so they are not written anywhere.
    /// </summary>
    /// <param name="newSerialNumber">
    ///     Pre-computed by the caller from the item's catalog rarity tier via the (not yet modeled in Fenrir)
    ///     time-derived formula at function.h:5-26; expected to be 0 for genuine pet-category items, which fall
    ///     outside the small set of rarity tiers that formula ever yields a non-zero result for.
    /// </param>
    public static WithdrawResult ResolveWithdrawToInventory(
        int sourceBagSlot, int? sourceBagItemId,
        byte destinationInventoryContainer, int destinationInventorySlot, int destinationX, int destinationY,
        ItemStack? destination, int newSerialNumber,
        bool petEquipped, bool bagUpperHalfEntitlementActive, bool secondInventoryPageEntitlementActive)
    {
        if (!IsValidBagSlot(sourceBagSlot))
            return WithdrawFail(TransferOutcome.SourceOutOfRange);

        if (!ContainerMatrix.IsValidSlot(destinationInventoryContainer, destinationInventorySlot))
            return WithdrawFail(TransferOutcome.DestinationOutOfRange);

        if (destinationX is < 0 or > 7 || destinationY is < 0 or > 7)
            return WithdrawFail(TransferOutcome.DestinationCoordinateOutOfRange);

        if (!petEquipped)
            return WithdrawFail(TransferOutcome.PetNotEquipped);

        if (RequiresUpperHalfEntitlement(sourceBagSlot) && !bagUpperHalfEntitlementActive)
            return WithdrawFail(TransferOutcome.PetBagUpperHalfExpired);

        if (destinationInventoryContainer == ContainerMatrix.InventoryPage1 && !secondInventoryPageEntitlementActive)
            return WithdrawFail(TransferOutcome.SecondInventoryPageExpired);

        if (sourceBagItemId is not { } itemId)
            return WithdrawFail(TransferOutcome.SourceEmpty);

        if (destination is not null)
            return WithdrawFail(TransferOutcome.DestinationOccupied);

        var newSlot = new ItemStack(itemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, newSerialNumber);
        return new WithdrawResult(TransferOutcome.Success, null, newSlot, true);
    }

    /// <summary>tSort 256 -- pet-bag slot to pet-bag slot rearrange.</summary>
    public static RearrangeResult ResolveRearrangeWithinPetBag(
        int sourceBagSlot, int destinationBagSlot,
        int? sourceBagItemId, int? destinationBagItemId, byte sourceItemSort,
        bool petEquipped, bool bagUpperHalfEntitlementActive)
    {
        if (!IsValidBagSlot(sourceBagSlot))
            return RearrangeFail(TransferOutcome.SourceOutOfRange);

        if (!IsValidBagSlot(destinationBagSlot))
            return RearrangeFail(TransferOutcome.DestinationOutOfRange);

        if (sourceBagSlot == destinationBagSlot)
            return new RearrangeResult(TransferOutcome.NoOp, sourceBagItemId, destinationBagItemId);

        if (!petEquipped)
            return RearrangeFail(TransferOutcome.PetNotEquipped);

        if ((RequiresUpperHalfEntitlement(sourceBagSlot) || RequiresUpperHalfEntitlement(destinationBagSlot)) &&
            !bagUpperHalfEntitlementActive)
            return RearrangeFail(TransferOutcome.PetBagUpperHalfExpired);

        if (sourceBagItemId is not { } itemId)
            return RearrangeFail(TransferOutcome.SourceEmpty);

        if (sourceItemSort != PetBagEligibleSort)
            return RearrangeFail(TransferOutcome.SourceItemNotPetEligible);

        if (destinationBagItemId is not null)
            return RearrangeFail(TransferOutcome.DestinationOccupied);

        return new RearrangeResult(TransferOutcome.Success, null, itemId);
    }

    private static DepositResult DepositFail(TransferOutcome outcome)
    {
        return new DepositResult(outcome, null, 0, false);
    }

    private static WithdrawResult WithdrawFail(TransferOutcome outcome)
    {
        return new WithdrawResult(outcome, null, null, false);
    }

    private static RearrangeResult RearrangeFail(TransferOutcome outcome)
    {
        return new RearrangeResult(outcome, null, null);
    }

    /// <summary>
    ///     NewGeneralInventorySlot is always <see langword="null" /> on success (ClearInventory's full reset);
    ///     NewPetBagItemId is the catalog id to store in the destination bag slot, meaningful only when
    ///     <see cref="Succeeded" />. ShouldEmitAuditLog is <see langword="true" /> exactly when Outcome is
    ///     <see cref="TransferOutcome.Success" /> (Direction A always logs on success, no bypass path exists).
    /// </summary>
    public readonly record struct DepositResult(
        TransferOutcome Outcome,
        ItemStack? NewGeneralInventorySlot,
        int NewPetBagItemId,
        bool ShouldEmitAuditLog)
    {
        public bool Succeeded => Outcome is TransferOutcome.Success;
    }

    /// <summary>
    ///     NewSourcePetBagItemId is always <see langword="null" /> on success (the bag slot reset to empty);
    ///     NewGeneralInventorySlot is the populated destination slot, meaningful only when <see cref="Succeeded" />.
    ///     ShouldEmitAuditLog mirrors <see cref="DepositResult.ShouldEmitAuditLog" />'s posture.
    /// </summary>
    public readonly record struct WithdrawResult(
        TransferOutcome Outcome,
        int? NewSourcePetBagItemId,
        ItemStack? NewGeneralInventorySlot,
        bool ShouldEmitAuditLog)
    {
        public bool Succeeded => Outcome is TransferOutcome.Success;
    }

    /// <summary>
    ///     On <see cref="TransferOutcome.NoOp" />, both fields echo back whatever the caller passed in (same
    ///     physical slot, so they're the same value) -- mirrors <see cref="SaveBankItemTransferPolicy" />'s own
    ///     same-slot NoOp precedent: the caller must not treat a NoOp's fields as "clear the slot". Direction C never
    ///     emits an audit-log call, on any path (verified directly -- the only one of the three cited functions with
    ///     no logging call in its body), so unlike Deposit/Withdraw there is no ShouldEmitAuditLog field here.
    /// </summary>
    public readonly record struct RearrangeResult(
        TransferOutcome Outcome,
        int? NewSourcePetBagItemId,
        int? NewDestinationPetBagItemId)
    {
        public bool Succeeded => Outcome is TransferOutcome.Success or TransferOutcome.NoOp;
    }
}
