using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Bridges the tick-owned, mutable <see cref="PlayerRuntimeState" /> to the pure
///     <see cref="HighLevelExperienceResolver" />, which deliberately has no dependency on it (its own class
///     doc: "No I/O or state dependency: every input is a value the caller already holds"). This is the
///     "B17-rebirth-hook" adapter -- the piece a later Domain-combat integration pass needs at the two award
///     chokepoints (<c>Zone.GrantMonsterKillExperience</c>/<c>Zone.ApplyPvpKillExperience</c>, both funneling
///     through the shared <c>Zone.Combat.cs</c> <c>ApplyCharacterExperienceGain</c> cascade) so neither call
///     site needs to hand-assemble a <see cref="HighLevelExperienceInput" /> itself.
///     <para>Read-only: never mutates <c>target</c>.</para>
/// </summary>
public static class HighLevelExperienceInputFactory
{
    /// <summary>
    ///     Builds the resolver's input from a character's current runtime counters and the world's own
    ///     level-cap row. <paramref name="levelsByLevel" /> is looked up once, for the general-level cap row's
    ///     own <c>ExpRangeMin</c> (Stage A's percentage-band lower endpoint) -- when that row is absent,
    ///     <see cref="HighLevelExperienceInput.MainExperienceFloor" /> falls back to 0, matching
    ///     <see cref="HighLevelExperienceResolver" />'s own documented contract for that field ("Pass 0 when
    ///     the cap row is unavailable").
    /// </summary>
    /// <param name="target">The awarding character's own runtime state (read-only here).</param>
    /// <param name="awardedExperience">The original, un-clamped experience amount this award is granting.</param>
    /// <param name="antiCheatExperienceFlagged">
    ///     The per-session anti-cheat experience flag. No <see cref="PlayerRuntimeState" /> field models this
    ///     yet (see <see cref="HighLevelExperienceInput.AntiCheatExperienceFlagged" />'s own remarks) -- pass
    ///     <see langword="false" /> until a future workstream wires a real source, honored if ever supplied.
    /// </param>
    /// <param name="levelsByLevel">The world's level table (<c>WorldDataCache.LevelsByLevel</c>).</param>
    public static HighLevelExperienceInput Build(PlayerRuntimeState target, int awardedExperience,
        bool antiCheatExperienceFlagged, FrozenDictionary<short, LevelRowDto> levelsByLevel)
    {
        var mainExperienceFloor = levelsByLevel.TryGetValue(LevelProgressionCalculator.MaxLevel, out var maxLevelRow)
            ? maxLevelRow.ExpRangeMin
            : 0;

        return new HighLevelExperienceInput(
            target.Level,
            target.Experience,
            mainExperienceFloor,
            target.Level2,
            target.Exp2,
            awardedExperience,
            antiCheatExperienceFlagged);
    }
}
