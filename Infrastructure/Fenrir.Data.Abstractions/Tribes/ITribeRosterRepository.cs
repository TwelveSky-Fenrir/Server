using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Tribes;

/// <summary>
///     The persisted character/avatar roster read behind the level/rebirth-based tribe-point recompute
///     (C17 Part C). Kept as its own single-purpose repository rather than folded into
///     <see cref="ITribeRepository" /> so the recompute's roster-scan seam is independent of the tribe
///     master/sub-master/bank write surface.
/// </summary>
public interface ITribeRosterRepository
{
    /// <summary>
    ///     Every persisted character that clears the level-145 max-level gate, each carrying tribe, primary
    ///     level, secondary level and rebirth count (game.usp_TribeRoster_GetForTribePoint). The pure
    ///     per-tribe totals formula (baseline 1000, per-character three-term sum, tribe-3 +800) is applied by
    ///     the domain, not here. An empty collection is a legitimate result (no max-level characters yet) --
    ///     the domain formula still produces every tribe's baseline from it.
    /// </summary>
    public ValueTask<ReadOnlyCollection<TribeRosterCharacterDto>> GetForTribePointAsync(CancellationToken ct);
}
