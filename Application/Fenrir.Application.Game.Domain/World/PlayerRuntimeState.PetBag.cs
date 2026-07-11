using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     wAvatar's 20-slot pet/animal cargo bag (<c>aPetBagDate</c>-adjacent array, Server/Header/Protocol/
///     STRUCT.h:520-521) -- a bare catalog-id-per-slot store with no quantity/value/serial/socket/expiration
///     fields at all, unlike every general-inventory/Store/Save slot. Same "absence = empty" convention as
///     <see cref="Hotkeys" />/<see cref="Inventory" />'s own per-container dictionaries. Mutated only by
///     <see cref="Zone" />'s own tick (via <c>Zone.PetBagCommands.cs</c>'s
///     <c>ApplyPetBagCommand</c>), same single-writer contract as every other field on this type.
/// </summary>
/// <remarks>
///     Open gap, flagged not silently left: unlike <see cref="Hotkeys" /> (hydrated at world entry from
///     <c>game.CharacterHotkeys</c> via <c>PlayerEnterData.Hotkeys</c>), this bag has no world-entry hydration
///     path yet -- <c>PlayerEnterData</c> carries no PetBag field, so every new/transferred
///     <see cref="PlayerRuntimeState" /> starts with an empty bag regardless of what
///     <c>game.CharacterPetBag</c> actually holds for that character, until a follow-up wiring pass adds that
///     field the same "must travel here or it silently resets" way <c>PlayerEnterData</c>'s own remarks
///     already document for a dozen other fields. The C8 behavior contract's own wiring manifest calls this
///     out explicitly rather than leaving it an undocumented surprise.
/// </remarks>
public partial class PlayerRuntimeState
{
    public ImmutableDictionary<byte, int> PetBag { get; set; } = ImmutableDictionary<byte, int>.Empty;

    /// <summary>
    ///     aPetBagDate (YYYYMMDD, <see cref="Simulation.GameDate" /> encoding) -- the pet-bag upper-half (slots
    ///     10-19, <c>PetBagItemTransferPolicy.UpperHalfStartSlot</c>) rental-entitlement
    ///     expiry, Server/Header/Protocol/STRUCT.h:520-521. Compared against
    ///     <see cref="Simulation.GameDate.Today" /> the same way <see cref="InventoryDate" />/
    ///     <see cref="StoreDate" /> gate their own second-page entitlements
    ///     (<c>GenericActionHandler</c>'s <c>petBagUpperHalfEntitlementActive</c> local,
    ///     <c>state.PetBagDate &gt;= GameDate.Today()</c>). A stale/never-granted value (0) reads as expired,
    ///     matching legacy: 0 &lt; any real YYYYMMDD date.
    /// </summary>
    /// <remarks>
    ///     C8 pet-bag-entitlement confirmation-pass follow-up: previously this field did not exist at all, so
    ///     <c>GenericActionHandler</c> hardcoded the entitlement to the literal <see langword="true" />
    ///     (fail-open) -- see that handler's own remarks for the prior state. Hydrated at world entry
    ///     (<c>EnterWorldService</c>, from <c>game.Characters.PetBagDate</c>,
    ///     Migrations/047_characters_petbagdate_column.sql) and carried through an in-process zone transfer
    ///     (<see cref="ZoneTransfer.CreateEnterData" />), same posture as <see cref="InventoryDate" />/
    ///     <see cref="StoreDate" />.
    ///     <para>
    ///         Unlike InventoryDate/StoreDate (which <c>usp_Character_CreateWithStarterKit</c>'s own
    ///         EU33/USE_CUSTOME_CREATE grant sets to a real 7-day welcome-bonus expiry at creation,
    ///         Server/ts25login/S04_MyWork02.cpp:740-1179), this field is deliberately left at its 0 default by
    ///         every creation path -- ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:381-383
    ///         confirms legacy's own live creation path never sets <c>aPetBagDate</c> either, so a character
    ///         reads this as permanently expired in both Fenrir and legacy until some other, not-yet-cited
    ///         mechanism (item/command) grants it. No citation anywhere (Server/ or ServerDocs/) establishes
    ///         what that mechanism is or what duration it would grant -- left as an explicit open question
    ///         rather than invented; do not add a grant path here without a real citation.
    ///     </para>
    /// </remarks>
    public int PetBagDate { get; set; }

    public int? GetPetBagSlot(byte slot)
    {
        return PetBag.TryGetValue(slot, out var itemId) ? itemId : null;
    }

    /// <summary>A null <paramref name="itemId" /> clears the slot entirely rather than storing a 0.</summary>
    public void SetPetBagSlot(byte slot, int? itemId)
    {
        PetBag = itemId is { } id ? PetBag.SetItem(slot, id) : PetBag.Remove(slot);
    }
}
