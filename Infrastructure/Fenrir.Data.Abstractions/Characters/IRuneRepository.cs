using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Characters;

/// <summary>
///     CaeriusNet wrapper around <c>game.CharacterRunes</c> -- the durable per-character rune-socket state
///     (legacy <c>aRuneSystem[4]</c>/<c>aRuneSystemStat[4]</c>) behind CZ_RUNE_SYSTEM_SEND (op157). A dedicated,
///     rare transactional store, deliberately kept off <see cref="ICharacterRepository" />'s hot write-behind
///     path: rune changes are player-initiated, one socket at a time, and each is atomic with an inventory
///     mutation -- never per-tick position/progress state (see <c>game.usp_Character_PersistRunes</c>'s own
///     header).
/// </summary>
public interface IRuneRepository
{
    /// <summary>
    ///     <c>usp_Character_GetRunes</c>: the occupied rune sockets for one character, for world-entry hydration
    ///     into <c>PlayerRuntimeState.RuneSystem</c>/<c>RuneSystemStat</c>. A never-socketed character returns
    ///     zero rows (every socket defaults to empty). Deliberately a separate read from
    ///     <c>usp_Character_GetForWorldEntry</c> -- see that procedure's own header for why the world-entry
    ///     bundle stays untouched.
    /// </summary>
    public ValueTask<ReadOnlyCollection<CharacterRuneSocketDto>> GetRunesAsync(int characterId, CancellationToken ct);

    /// <summary>
    ///     <c>usp_Character_PersistRunes</c>: replaces the whole 4-socket rune state AND (optionally) the one
    ///     paired inventory container in ONE transaction, so a rune equip/unequip can never dupe or lose the
    ///     rune item across the socket&lt;-&gt;inventory boundary -- the same atomicity the legacy in-process
    ///     swap guarantees (Server/ts25zone/S04_MyWork03.cpp:8162-8328).
    /// </summary>
    /// <param name="runes">
    ///     The whole rune array (occupied sockets only). An empty list clears every socket (the procedure's
    ///     DELETE runs unconditionally); the caller must OMIT the TVP for an empty list, never send a zero-row
    ///     one -- same empty-TVP rule as <see cref="ICharacterRepository.ReplaceContainerAsync" />.
    /// </param>
    /// <param name="container">
    ///     The inventory container the rune moved to/from. <c>null</c> leaves inventory untouched (a rune-only
    ///     re-anchor, passed to the procedure as the reserved 255 sentinel).
    /// </param>
    /// <param name="items">
    ///     That container's full post-mutation contents (whole-container replace). Empty = deliberate clear;
    ///     omitted when either <paramref name="container" /> is <c>null</c> or the list is empty.
    /// </param>
    public ValueTask PersistRunesAsync(int characterId, IReadOnlyList<CharacterRuneSocketTvp> runes,
        byte? container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);
}
