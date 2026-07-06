using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Domain.Hotkeys;

/// <summary>
///     Pure, Zone-independent policy for <c>CZ_PROCESS_DATA_SEND</c>'s hotkey-bar family: tSort 204 (bind
///     skill/emoticon), 205 (unbind), 211/253 (bind inventory item), 214 (withdraw item to inventory), and 216
///     (rearrange hotkey-to-hotkey). No I/O, no Zone/PlayerRuntimeState dependency -- same posture as
///     <see cref="Inventory.ContainerMatrix" /> and <see cref="Inventory.StoreItemTransferPolicy" />, and, like
///     those two, not wired into any handler/service yet.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:591-666 (<c>ProcessForSkillToHotKey</c>, opcode 204) ; :668-703
///     (<c>ProcessForHotKeyToNo</c>, opcode 205) ; :1308-1396 (<c>ProcessForInventoryToHotKey</c>, opcodes
///     211/253) ; :1626-1714 (<c>ProcessForHotKeyToInventory</c>, opcode 214, including the source-page
///     bound-check bug at line 1628) ; :2030-2134 (<c>ProcessForHotKeyToHotKey</c>, opcode 216) ; :65-70
///     (<c>ClearHotkey</c>) ; :122-159 (<c>HOTKEY_SORT</c> enum and its 3 tagged-union views) ; :161-169
///     (<c>SKILL_WORK</c>/<c>GetSkillForWork</c>) ; :239-246 (<c>CheckMinMax</c>, the shared [min,max) bound
///     helper every one of these five uses) ; Server/ts25zone/S04_MyWork04.cpp:402-419, 441-483, 512-518,
///     537-559 (dispatch entries, confirming 253 is a verbatim alias of 211) ; Server/Header/Protocol/DEFINE.h:
///     287-303 (MAX_INVENTORY_PAGE_NUM=2, MAX_INVENTORY_SLOT_NUM=64, MAX_SKILL_SLOT_NUM=40, MAX_HOT_KEY_PAGE=3,
///     MAX_HOT_KEY_NUM=14) ; DEFINE.h:611 (MAX_ITEM_DUPLICATION_NUM=999).
///     <para>
///         Every failure branch across all five legacy operations is a disconnect (Quit()), never a graceful
///         reply -- same posture as <see cref="Skills.SkillLearnResolver" />'s own remarks. Callers must abort
///         the session on any non-success result; there is no clean "reply with a failure code" path for this
///         family (confirmed by the behavior contract's own Error/failure semantics section).
///     </para>
///     <para>
///         Two legacy specifics are deliberately NOT reproduced here, each a documented, explicit deviation
///         rather than a silent one -- see <see cref="ResolveWithdrawItem" /> and <see cref="ResolveBindItem" />
///         /<see cref="ResolveRearrange" />'s own remarks for each.
///     </para>
/// </remarks>
public static class HotkeyActionResolver
{
    public enum BindEmoticonFailure
    {
        None,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        InvalidCode,
        DestinationOccupied
    }

    public enum BindItemFailure
    {
        None,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        InvalidSourcePage,
        InvalidSourceIndex,
        SourceEmpty,
        NotStackable,
        ExcludedPotionSubtype,
        InvalidQuantity,
        InsufficientSourceQuantity,
        DestinationItemMismatch,
        DestinationOverCap
    }

    public enum BindSkillFailure
    {
        None,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        InvalidSkillSlot,
        SkillSlotEmpty,
        InvalidGrade,
        DestinationOccupied
    }

    public enum RearrangeFailure
    {
        None,
        InvalidSourcePage,
        InvalidSourceIndex,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        SourceEmpty,
        DestinationOccupied,
        InvalidQuantity,
        InsufficientSourceQuantity,
        DestinationItemMismatch,
        DestinationOverCap
    }

    public enum UnbindFailure
    {
        None,
        InvalidPage,
        InvalidIndex,
        AlreadyEmpty,
        ItemBindingNotSupported
    }

    public enum WithdrawItemFailure
    {
        None,
        InvalidSourcePage,
        InvalidSourceIndex,
        SourceEmpty,
        SourceNotItem,
        InvalidQuantity,
        InsufficientSourceQuantity,
        InvalidDestinationPage,
        InvalidDestinationIndex,
        InvalidDestinationX,
        InvalidDestinationY,
        DestinationItemMismatch,
        DestinationOverCap
    }

    /// <summary>MAX_HOT_KEY_PAGE.</summary>
    public const int PageCount = 3;

    /// <summary>MAX_HOT_KEY_NUM.</summary>
    public const int SlotsPerPage = 14;

    /// <summary>A closed set of 9 fixed emoticon codes, 1-9 inclusive -- no catalog lookup involved.</summary>
    public const int MinEmoticonCode = 1;

    public const int MaxEmoticonCode = 9;

    /// <summary>The client-supplied grade for a skill bind must be at least this.</summary>
    public const int MinSkillGrade = 1;

    /// <summary>MAX_ITEM_DUPLICATION_NUM -- a hard rejection on overflow, never a clamp.</summary>
    public const int MaxItemQuantity = 999;

    public const int MinItemQuantity = 1;

    public static bool IsValidPage(int page)
    {
        return page is >= 0 and < PageCount;
    }

    public static bool IsValidIndex(int index)
    {
        return index is >= 0 and < SlotsPerPage;
    }

    /// <summary>
    ///     tSort 204, skill branch. <paramref name="skillSlotIndex" /> indexes the character's own 40 skill
    ///     slots (<see cref="SkillLearnResolver.MaxSlots" />), not a catalog id -- the catalog id actually bound
    ///     is whatever is currently stored in that slot. Binding never mutates the referenced skill slot itself
    ///     (no resource is consumed).
    /// </summary>
    /// <param name="destination">The destination hotkey slot's current content.</param>
    public static BindSkillResult ResolveBindSkill(
        HotkeySlot destination, int destinationPage, int destinationIndex,
        int skillSlotIndex, int requestedGrade,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        if (!IsValidPage(destinationPage))
            return BindSkillResult.Fail(BindSkillFailure.InvalidDestinationPage);

        if (!IsValidIndex(destinationIndex))
            return BindSkillResult.Fail(BindSkillFailure.InvalidDestinationIndex);

        if (skillSlotIndex < 0 || skillSlotIndex >= SkillLearnResolver.MaxSlots)
            return BindSkillResult.Fail(BindSkillFailure.InvalidSkillSlot);

        if (!learnedSkills.TryGetValue((byte)skillSlotIndex, out var learned))
            return BindSkillResult.Fail(BindSkillFailure.SkillSlotEmpty);

        if (requestedGrade < MinSkillGrade || requestedGrade > learned.Grade)
            return BindSkillResult.Fail(BindSkillFailure.InvalidGrade);

        // Skill/emoticon destinations must be entirely empty -- occupied by anything at all (skill, emoticon,
        // or item) blocks the write. Unlike an item destination (see ResolveBindItem), there is no
        // unconditional-overwrite path here.
        if (!destination.IsEmpty)
            return BindSkillResult.Fail(BindSkillFailure.DestinationOccupied);

        var newDestination = new HotkeySlot(HotkeyBindingKind.Skill, learned.SkillId, requestedGrade);
        return new BindSkillResult(true, BindSkillFailure.None, newDestination);
    }

    /// <summary>
    ///     tSort 204, emoticon branch. The wire field reused as a skill-slot index for the skill branch instead
    ///     carries a literal fixed emoticon id here (1-9) -- no catalog lookup. The stored grade is always
    ///     forced to 0 regardless of anything the client sends, so this overload takes no grade parameter at
    ///     all.
    /// </summary>
    public static BindEmoticonResult ResolveBindEmoticon(
        HotkeySlot destination, int destinationPage, int destinationIndex, int emoticonCode)
    {
        if (!IsValidPage(destinationPage))
            return BindEmoticonResult.Fail(BindEmoticonFailure.InvalidDestinationPage);

        if (!IsValidIndex(destinationIndex))
            return BindEmoticonResult.Fail(BindEmoticonFailure.InvalidDestinationIndex);

        if (emoticonCode < MinEmoticonCode || emoticonCode > MaxEmoticonCode)
            return BindEmoticonResult.Fail(BindEmoticonFailure.InvalidCode);

        if (!destination.IsEmpty)
            return BindEmoticonResult.Fail(BindEmoticonFailure.DestinationOccupied);

        var newDestination = new HotkeySlot(HotkeyBindingKind.Emoticon, emoticonCode, 0);
        return new BindEmoticonResult(true, BindEmoticonFailure.None, newDestination);
    }

    /// <summary>
    ///     tSort 205. Not idempotent: an already-empty slot is rejected exactly like any other invalid input,
    ///     there is no "already cleared" no-op success. Clearing an item binding is deliberately unsupported on
    ///     this path -- <see cref="ResolveWithdrawItem" /> (tSort 214) must be used instead, since only that
    ///     path returns the item's value to the inventory rather than discarding it.
    /// </summary>
    public static UnbindResult ResolveUnbind(HotkeySlot slot, int page, int index)
    {
        if (!IsValidPage(page))
            return UnbindResult.Fail(UnbindFailure.InvalidPage);

        if (!IsValidIndex(index))
            return UnbindResult.Fail(UnbindFailure.InvalidIndex);

        if (slot.IsEmpty)
            return UnbindResult.Fail(UnbindFailure.AlreadyEmpty);

        if (slot.Kind == HotkeyBindingKind.Item)
            return UnbindResult.Fail(UnbindFailure.ItemBindingNotSupported);

        return UnbindResult.Succeeded;
    }

    /// <summary>
    ///     tSort 211/253 -- wire-level aliases dispatched to identical logic (253 exists only so the legacy
    ///     client can distinguish a drag vs. a right-click gesture; there is no branch here or in the legacy
    ///     source that treats them differently). <paramref name="sourceItemIsStackable" />/
    ///     <paramref name="sourceItemIsExcludedPotionSubtype" /> are caller-resolved against the item catalog --
    ///     this pure policy never touches one itself, same posture as <see cref="StoreItemTransferPolicy" />'s
    ///     own caller-supplied catalog flags.
    /// </summary>
    /// <remarks>
    ///     <paramref name="sourceItemIsExcludedPotionSubtype" />: the legacy source refuses two specific
    ///     hardcoded potion-subtype codes, but the behavior contract this resolver implements explicitly does
    ///     not state their semantic meaning or value ("not covered by the cited range") -- no specific values
    ///     are guessed here per the "no legacy parity from memory" rule. The caller should default this to
    ///     <see langword="false" /> until a follow-up legacy-behavior-translator finding supplies the two
    ///     codes; doing so never rejects a legitimate bind, it only leaves this one specific exclusion
    ///     unenforced in the interim.
    ///     <para>
    ///         Quantity-pollution defect (NOT reproduced): when <paramref name="destination" /> currently holds
    ///         a Skill or Emoticon binding, the legacy code unconditionally overwrites it but adds the
    ///         requested quantity onto the destination's stale second raw value instead of resetting it first --
    ///         for a Skill destination (whose second value holds a bound grade, always &gt;= 1) this inflates
    ///         the resulting item quantity by that leftover grade with no corresponding debit anywhere (e.g.
    ///         overwriting a grade-5 skill binding with a request to move 10 units leaves the slot reporting 15,
    ///         not 10); an Emoticon destination's second value is always 0, so the same arithmetic happens to be
    ///         correct there. The behavior contract flags this as a genuine defect and explicitly leaves the
    ///         fix-or-reproduce choice to the implementer. This resolver FIXES it: an unconditional overwrite of
    ///         a Skill/Emoticon destination always assigns the requested quantity outright
    ///         (<paramref name="requestedQuantity" />), never adding it onto the stale second value -- a
    ///         deliberate, documented deviation from legacy, not a silent one.
    ///     </para>
    /// </remarks>
    public static BindItemResult ResolveBindItem(
        HotkeySlot destination, int destinationPage, int destinationIndex,
        ItemStack? sourceItem, int sourcePage, int sourceIndex, int requestedQuantity,
        bool sourceItemIsStackable, bool sourceItemIsExcludedPotionSubtype)
    {
        if (!IsValidPage(destinationPage))
            return BindItemResult.Fail(BindItemFailure.InvalidDestinationPage);

        if (!IsValidIndex(destinationIndex))
            return BindItemResult.Fail(BindItemFailure.InvalidDestinationIndex);

        if (sourcePage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1))
            return BindItemResult.Fail(BindItemFailure.InvalidSourcePage);

        if (!ContainerMatrix.IsValidSlot((byte)sourcePage, sourceIndex))
            return BindItemResult.Fail(BindItemFailure.InvalidSourceIndex);

        if (sourceItem is not { } source)
            return BindItemResult.Fail(BindItemFailure.SourceEmpty);

        if (!sourceItemIsStackable)
            return BindItemResult.Fail(BindItemFailure.NotStackable);

        if (sourceItemIsExcludedPotionSubtype)
            return BindItemResult.Fail(BindItemFailure.ExcludedPotionSubtype);

        if (requestedQuantity < MinItemQuantity || requestedQuantity > MaxItemQuantity)
            return BindItemResult.Fail(BindItemFailure.InvalidQuantity);

        if (requestedQuantity > source.Quantity)
            return BindItemResult.Fail(BindItemFailure.InsufficientSourceQuantity);

        int newQuantity;
        if (destination.Kind == HotkeyBindingKind.Item)
        {
            if (destination.Value1 != source.ItemId)
                return BindItemResult.Fail(BindItemFailure.DestinationItemMismatch);

            newQuantity = destination.Value2 + requestedQuantity;
            if (newQuantity > MaxItemQuantity)
                return BindItemResult.Fail(BindItemFailure.DestinationOverCap);
        }
        else
        {
            // Empty, Skill, or Emoticon destination: unconditionally overwritten -- see remarks for why this
            // does NOT add onto a Skill destination's stale second value.
            newQuantity = requestedQuantity;
        }

        var newDestination = new HotkeySlot(HotkeyBindingKind.Item, source.ItemId, newQuantity);
        var remainingSourceQuantity = source.Quantity - requestedQuantity;

        return new BindItemResult(true, BindItemFailure.None, newDestination, remainingSourceQuantity);
    }

    /// <summary>
    ///     tSort 214 -- moves a quantity of an item-type hotkey binding back into an inventory slot. Rejects a
    ///     source slot that isn't currently an Item binding (the reciprocal of <see cref="ResolveUnbind" />
    ///     refusing an Item binding).
    /// </summary>
    /// <remarks>
    ///     Legacy source-page bound-check defect (NOT reproduced): Server/ts25zone/S04_MyWork05.cpp:1628
    ///     validates the source page against MAX_HOT_KEY_NUM (14) instead of MAX_HOT_KEY_PAGE (3), so legacy
    ///     page values 3-13 pass validation there. Because the legacy per-character hotkey block and the
    ///     immediately-following quest-progress block are laid out contiguously with no padding (Server/Header/
    ///     Protocol/STRUCT.h:364-366), an out-of-range page still computes a syntactically valid address that
    ///     lands inside that adjacent quest-progress memory, both on the initial read and on the final
    ///     zero-fill of a fully-withdrawn slot. This is specific to opcode 214 -- the equivalent page check on
    ///     opcodes 204/205/211/253/216 all correctly use the page-count constant. Fenrir's <see cref="HotkeySlot" />
    ///     storage has no contiguous adjacent-field memory to read or corrupt this way, so there is nothing
    ///     analogous to reproduce for page values 3-13 -- this resolver validates
    ///     <paramref name="sourcePage" /> against the correct page-count bound (<see cref="IsValidPage" />, 0-2)
    ///     instead of the legacy's wider, memory-unsafe range. The behavior contract itself notes a full
    ///     exploitability analysis of the legacy defect was not carried out and flags it for
    ///     fenrir-security-hardening-engineer rather than assuming it is merely cosmetic; this deviation is
    ///     recorded here for that follow-up.
    /// </remarks>
    public static WithdrawItemResult ResolveWithdrawItem(
        HotkeySlot source, int sourcePage, int sourceIndex, int requestedQuantity,
        ItemStack? destinationItem, int destinationPage, int destinationIndex,
        int destinationX, int destinationY)
    {
        if (!IsValidPage(sourcePage))
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidSourcePage);

        if (!IsValidIndex(sourceIndex))
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidSourceIndex);

        if (source.IsEmpty)
            return WithdrawItemResult.Fail(WithdrawItemFailure.SourceEmpty);

        if (source.Kind != HotkeyBindingKind.Item)
            return WithdrawItemResult.Fail(WithdrawItemFailure.SourceNotItem);

        if (requestedQuantity < MinItemQuantity || requestedQuantity > MaxItemQuantity)
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidQuantity);

        if (requestedQuantity > source.Value2)
            return WithdrawItemResult.Fail(WithdrawItemFailure.InsufficientSourceQuantity);

        if (destinationPage is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1))
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidDestinationPage);

        if (!ContainerMatrix.IsValidSlot((byte)destinationPage, destinationIndex))
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidDestinationIndex);

        if (destinationX < 0 || destinationX > 7)
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidDestinationX);

        if (destinationY < 0 || destinationY > 7)
            return WithdrawItemResult.Fail(WithdrawItemFailure.InvalidDestinationY);

        int newDestinationQuantity;
        if (destinationItem is { } destination)
        {
            if (destination.ItemId != source.Value1)
                return WithdrawItemResult.Fail(WithdrawItemFailure.DestinationItemMismatch);

            newDestinationQuantity = destination.Quantity + requestedQuantity;
            if (newDestinationQuantity > MaxItemQuantity)
                return WithdrawItemResult.Fail(WithdrawItemFailure.DestinationOverCap);
        }
        else
        {
            newDestinationQuantity = requestedQuantity;
        }

        var remaining = source.Value2 - requestedQuantity;
        var newSource = remaining > 0 ? source with { Value2 = remaining } : HotkeySlot.Empty;

        return new WithdrawItemResult(true, WithdrawItemFailure.None, newSource, source.Value1,
            newDestinationQuantity);
    }

    /// <summary>
    ///     tSort 216 -- moves one hotkey slot's entire content into another, for any binding kind. A Skill or
    ///     Emoticon source always moves as a whole, indivisible unit (<paramref name="requestedQuantity" /> is
    ///     meaningless and ignored in that branch); an Item source moves the requested quantity, with the same
    ///     merge/cap/unconditional-overwrite rules as <see cref="ResolveBindItem" /> (including the same
    ///     quantity-pollution fix -- see that method's remarks, which apply identically here).
    /// </summary>
    /// <remarks>
    ///     Same-slot request (source and destination address the identical page+index): the behavior contract
    ///     does not cover this edge case explicitly. Blindly applying the documented "copy destination from
    ///     source, then zero source" steps to identical addresses would destructively erase the binding
    ///     (copy, then immediately zero the same slot). This resolver instead treats an identical source/
    ///     destination address as a no-mutation success, by analogy with
    ///     <see cref="Inventory.ContainerMatrix.ResolveMove" />'s and <see cref="StoreItemTransferPolicy" />'s
    ///     own identical-slot NoOp handling for the sibling container-move families -- an inferred-by-analogy
    ///     design choice, not a specific legacy citation, flagged here for verification if byte-exact parity on
    ///     this exact point ever matters.
    /// </remarks>
    public static RearrangeResult ResolveRearrange(
        HotkeySlot source, int sourcePage, int sourceIndex,
        HotkeySlot destination, int destinationPage, int destinationIndex,
        int requestedQuantity)
    {
        if (!IsValidPage(sourcePage))
            return RearrangeResult.Fail(RearrangeFailure.InvalidSourcePage);

        if (!IsValidIndex(sourceIndex))
            return RearrangeResult.Fail(RearrangeFailure.InvalidSourceIndex);

        if (!IsValidPage(destinationPage))
            return RearrangeResult.Fail(RearrangeFailure.InvalidDestinationPage);

        if (!IsValidIndex(destinationIndex))
            return RearrangeResult.Fail(RearrangeFailure.InvalidDestinationIndex);

        if (source.IsEmpty)
            return RearrangeResult.Fail(RearrangeFailure.SourceEmpty);

        if (sourcePage == destinationPage && sourceIndex == destinationIndex)
            return new RearrangeResult(true, RearrangeFailure.None, source, destination);

        if (source.Kind is HotkeyBindingKind.Skill or HotkeyBindingKind.Emoticon)
        {
            if (!destination.IsEmpty)
                return RearrangeResult.Fail(RearrangeFailure.DestinationOccupied);

            return new RearrangeResult(true, RearrangeFailure.None, HotkeySlot.Empty, source);
        }

        // Item branch.
        if (requestedQuantity < MinItemQuantity || requestedQuantity > MaxItemQuantity)
            return RearrangeResult.Fail(RearrangeFailure.InvalidQuantity);

        if (requestedQuantity > source.Value2)
            return RearrangeResult.Fail(RearrangeFailure.InsufficientSourceQuantity);

        int newQuantity;
        if (destination.Kind == HotkeyBindingKind.Item)
        {
            if (destination.Value1 != source.Value1)
                return RearrangeResult.Fail(RearrangeFailure.DestinationItemMismatch);

            newQuantity = destination.Value2 + requestedQuantity;
            if (newQuantity > MaxItemQuantity)
                return RearrangeResult.Fail(RearrangeFailure.DestinationOverCap);
        }
        else
        {
            newQuantity = requestedQuantity;
        }

        var remaining = source.Value2 - requestedQuantity;
        var newSource = remaining > 0 ? source with { Value2 = remaining } : HotkeySlot.Empty;
        var newDestination = new HotkeySlot(HotkeyBindingKind.Item, source.Value1, newQuantity);

        return new RearrangeResult(true, RearrangeFailure.None, newSource, newDestination);
    }

    public readonly record struct BindSkillResult(bool Success, BindSkillFailure Failure, HotkeySlot NewDestination)
    {
        public static BindSkillResult Fail(BindSkillFailure failure)
        {
            return new BindSkillResult(false, failure, default);
        }
    }

    public readonly record struct BindEmoticonResult(
        bool Success,
        BindEmoticonFailure Failure,
        HotkeySlot NewDestination)
    {
        public static BindEmoticonResult Fail(BindEmoticonFailure failure)
        {
            return new BindEmoticonResult(false, failure, default);
        }
    }

    /// <summary>On success the addressed slot's new content is always <see cref="HotkeySlot.Empty" />.</summary>
    public readonly record struct UnbindResult(bool Success, UnbindFailure Failure)
    {
        public static readonly UnbindResult Succeeded = new(true, UnbindFailure.None);

        public static UnbindResult Fail(UnbindFailure failure)
        {
            return new UnbindResult(false, failure);
        }
    }

    /// <summary>
    ///     <see cref="RemainingSourceQuantity" /> is the source inventory slot's quantity after the debit --
    ///     0 means the caller must clear that slot entirely (item identity, position, gem-socket data and
    ///     expiry date all reset to empty), matching the same "clear a slot" step used throughout this family.
    /// </summary>
    public readonly record struct BindItemResult(
        bool Success,
        BindItemFailure Failure,
        HotkeySlot NewDestination,
        int RemainingSourceQuantity)
    {
        public static BindItemResult Fail(BindItemFailure failure)
        {
            return new BindItemResult(false, failure, default, 0);
        }
    }

    /// <summary>
    ///     <see cref="NewDestinationItemId" />/<see cref="NewDestinationQuantity" /> are the values to write
    ///     into the destination inventory slot (a brand-new slot populated outright, or an existing matching
    ///     stack topped up) -- on/off-screen X/Y are validated but not modeled (Fenrir's <see cref="ItemStack" />
    ///     carries no such field, same posture as ground-item pickup's own X/Y bound-check-only handling).
    /// </summary>
    public readonly record struct WithdrawItemResult(
        bool Success,
        WithdrawItemFailure Failure,
        HotkeySlot NewSource,
        int NewDestinationItemId,
        int NewDestinationQuantity)
    {
        public static WithdrawItemResult Fail(WithdrawItemFailure failure)
        {
            return new WithdrawItemResult(false, failure, default, 0, 0);
        }
    }

    public readonly record struct RearrangeResult(
        bool Success,
        RearrangeFailure Failure,
        HotkeySlot NewSource,
        HotkeySlot NewDestination)
    {
        public static RearrangeResult Fail(RearrangeFailure failure)
        {
            return new RearrangeResult(false, failure, default, default);
        }
    }
}
