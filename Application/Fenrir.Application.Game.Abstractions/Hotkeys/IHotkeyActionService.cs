using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Hotkeys;

/// <summary>
///     Business logic behind <c>GenericActionHandler</c>'s CZ_PROCESS_DATA_SEND hotkey-bind-family dispatch:
///     tSort 204 (bind skill/emoticon), 205 (unbind), 211/253 (bind inventory item), 214 (withdraw item to
///     inventory), 216 (rearrange hotkey-to-hotkey). Every method returns the same
///     <see cref="GenericActionResult" /> shared by every other <c>IGenericActionService</c> family so
///     <c>GenericActionHandler</c>'s existing <c>Respond</c> helper handles the wire reply with no new mapping
///     code -- per the C8 behavior contract's own Error/failure semantics section, there is no soft/clean
///     failure reply anywhere in this family: every rejection is <see cref="GenericActionStatus.Aborted" />
///     (disconnect), never <see cref="GenericActionStatus.Failed" />.
/// </summary>
/// <remarks>
///     Réf. contract: C8-hotkey-money-petbag (legacy-behavior-translator, wave7) -- see
///     <c>Fenrir.Application.Game.Domain.Hotkeys.HotkeyActionResolver</c>'s own citation trail for the
///     underlying <c>Server/ts25zone/S04_MyWork05.cpp</c> ranges this orchestrates.
///     <para>
///         tSort 204/205 carry a dedicated wire payload distinct from the generic 7-field
///         <see cref="DefaultPData" /> shape every other tSort in this family (and most of the rest of
///         <c>GenericActionHandler</c>) reuses (Server/Header/Protocol/STRUCT.h:1248-1255 for 204, :1256-1260
///         for 205, per the C8 contract's own Inputs section) -- no such wire struct exists in this codebase
///         yet. <see cref="BindSkillAsync" />/<see cref="BindEmoticonAsync" />/<see cref="UnbindAsync" /> are
///         therefore shaped to take the already-decoded raw field values directly rather than a
///         <c>[FenrirWireType]</c> struct, so this whole family's Domain/Services logic can be implemented and
///         tested now; wiring <c>GenericActionHandler</c>'s own tSort 204/205 branches still needs
///         fenrir-wire-protocol-implementer to add the two dedicated payload structs first (tracked as an open
///         question on the C8 wiring pass, NOT guessed at here).
///     </para>
/// </remarks>
public interface IHotkeyActionService
{
    /// <summary>
    ///     tSort 204, skill branch. <paramref name="skillSlotIndex" /> indexes the character's own 40 skill
    ///     slots (not a catalog id) -- the catalog id actually bound is whatever is currently stored in that
    ///     slot. See <c>HotkeyActionResolver.ResolveBindSkill</c> for the full rule set.
    /// </summary>
    public ValueTask<GenericActionResult> BindSkillAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int skillSlotIndex, int requestedGrade, int destinationPage, int destinationIndex,
        CancellationToken cancellationToken);

    /// <summary>tSort 204, emoticon branch. See <c>HotkeyActionResolver.ResolveBindEmoticon</c>.</summary>
    public ValueTask<GenericActionResult> BindEmoticonAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int emoticonCode, int destinationPage, int destinationIndex, CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 205 -- clears a skill/emoticon hotkey slot. Rejects (disconnect) a slot holding an item
    ///     binding; item hotkeys are removed through <see cref="WithdrawItemAsync" /> instead. See
    ///     <c>HotkeyActionResolver.ResolveUnbind</c>.
    /// </summary>
    public ValueTask<GenericActionResult> UnbindAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int page, int index, CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 211/253 (wire-level aliases, identical logic) -- binds a stackable-consumable inventory item
    ///     onto a hotkey slot. <paramref name="move" />: Page1/Index1 = source inventory page/slot, Page2/Index2
    ///     = destination hotkey page/slot, Quantity1 = units to move. See
    ///     <c>HotkeyActionResolver.ResolveBindItem</c> for the item-sort-2-only and potion-type-7/8-exclusion
    ///     rules the C8 contract adds on top of that resolver's own pre-existing remarks.
    /// </summary>
    public ValueTask<GenericActionResult> BindItemAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 214 -- moves a quantity of an item-type hotkey binding back into an inventory slot.
    ///     <paramref name="move" />: Page1/Index1 = source hotkey page/index, Page2/Index2 = destination
    ///     inventory page/slot, XPost2/YPost2 = destination cell coordinates, Quantity1 = units to move. See
    ///     <c>HotkeyActionResolver.ResolveWithdrawItem</c> -- this direction applies no potion-type exclusion
    ///     and clamps the source hotkey page to the correct 0-2 bound (the legacy source-page bound-check
    ///     defect is NOT reproduced, per that resolver's own remarks).
    /// </summary>
    public ValueTask<GenericActionResult> WithdrawItemAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 216 -- moves one hotkey slot's entire content into another, for any binding kind.
    ///     <paramref name="move" />: Page1/Index1 = source hotkey page/index, Page2/Index2 = destination hotkey
    ///     page/index, Quantity1 = units to move (item branch only). A source-equals-destination request is a
    ///     bare no-mutation success -- neither the database nor the zone tick is touched. See
    ///     <c>HotkeyActionResolver.ResolveRearrange</c>.
    /// </summary>
    public ValueTask<GenericActionResult> RearrangeAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);
}
