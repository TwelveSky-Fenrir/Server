using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Tribes;

/// <summary>
///     Item-driven, in-place tribe reclassification via a consumable. Kept as its own single-purpose repository
///     rather than growing <see cref="Fenrir.Data.Abstractions.Characters.ICharacterRepository" /> because a
///     tribe conversion is a self-contained aggregate action (equip/skill/hotkey remap + tribe flip + item
///     consumption committed atomically), not part of the character CRUD surface.
///     <para>
///         Today this exposes only the faction-transfer SCROLL path. The distinct Book-of-Noble-Dragon/Royal-
///         Serpent/Grand-Tiger path already lives on <c>ICharacterRepository.ApplyTribeConversionAsync</c>
///         (game.usp_Character_ApplyTribeConversion) -- the two are genuinely different reclassifiers and must
///         never be conflated (all-or-nothing vs best-effort equipment remap; server-derived vs client-supplied
///         target tribe). See game.usp_Character_ApplyTribeScrollConversion's own header.
///     </para>
/// </summary>
public interface ITribeConversionRepository
{
    /// <summary>
    ///     Faction-transfer scroll conversion (world.Items 8153/8154) -- the TChangeTribe reclassifier, applied
    ///     atomically together with the scroll's consumption by game.usp_Character_ApplyTribeScrollConversion so
    ///     a mid-sequence failure can never grant the tribe change without consuming the scroll (or vice versa).
    ///     <paramref name="toTribe" /> is the client-supplied target tribe and is range-checked (0..2) inside
    ///     the procedure; the equipment remap is best-effort per slot (un-mappable gear is left as-is, never an
    ///     abort). <paramref name="container" />/<paramref name="items" /> is a whole-container replace of the
    ///     ONE inventory container the scroll was consumed from (same empty-list-omission rule as
    ///     <c>ICharacterRepository.ReplaceContainerAsync</c>), with the scroll's own slot simply absent from
    ///     <paramref name="items" />.
    /// </summary>
    /// <remarks>
    ///     The procedure enforces only the DB-checkable gates (item id, target-tribe range, character exists,
    ///     same-previous-tribe, level cap, tribe office, guild, friends -- THROW 50341-50348). The caller must
    ///     still verify the runtime-only gates itself before invoking (zone-37 connectivity, standing in the
    ///     current tribe's capital, no active party, no teacher/student, no equipped cape/cloak, and the
    ///     ts25extra cross-process target-tribe validation), none of which has durable representation in this
    ///     database -- see the stored procedure's own header for the full split.
    /// </remarks>
    public ValueTask ApplyTribeScrollConversionAsync(int characterId, int itemId, byte toTribe, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);
}
