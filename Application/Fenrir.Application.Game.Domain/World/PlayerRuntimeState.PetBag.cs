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
