using System.Collections.Immutable;

namespace Fenrir.Application.Game.Stats.Context;

/// <summary>
///     Mount/animal stat inputs the legacy <c>MyFactor</c> reads that the current equipment-only
///     <see cref="StatCalculator" /> surface has no channel for. Every field defaults to a neutral/zero value,
///     so a <c>default</c> instance reproduces "not mounted / no mount contribution".
///     <b>
///         This context
///         deliberately straddles both layers
///     </b>
///     : the grade multiplier and the absorbed-animal add feed the BASE
///     cache, while the mount runtime attributes add flat amounts on every EFFECTIVE resolution -- a consuming
///     workstream must preserve that split.
/// </summary>
/// <remarks>
///     Introduced by workstream B1 as a pure plumbing seam: threaded through
///     <see cref="StatCalculator.ComputeBaseStats" />/<see cref="StatCalculator.ComputeEffectiveStats" /> and
///     the base getters but not read by any formula yet (each contributes 0). Réf. legacy input surface: grade
///     multiplier <c>Server/Header/Protocol/MyFactor.cpp:1945-1963,2265-2274</c>
///     (<c>mAnimalInfo.base.hp</c> -&gt; <c>ANIMAL_RATE_*_GRADE</c>, a whole-value multiplier on max life/mana,
///     attack, defense, hit, block, crit and elemental stats -- note it does NOT touch Luck); absorb
///     <c>MyFactor.cpp:1705-1708</c> (<c>mAnimalInfo.absorb</c> gated by <c>aAnimalAbsorbState</c>, added to a
///     base primary stat); mount runtime attributes <c>MyFactor.cpp:4009-4143</c> (<c>mAnimalInfo.attr.dmg</c>/
///     <c>.def</c> flat effective adds).
/// </remarks>
/// <param name="AnimalNumber">
///     aAnimalNumber -- the currently-mounted animal id (0 = not mounted, <c>PlayerRuntimeState.AnimalNumber</c>).
/// </param>
/// <param name="AnimalGrade">
///     The resolved grade tier (1-4) that drives the BASE whole-value multiplier; 0 = none/unresolved. Derived
///     from the animal definition, so the assembler leaves this 0 until an animal catalog lookup exists.
/// </param>
/// <param name="AbsorbActive">
///     aAnimalAbsorbState != 0 (<c>PlayerRuntimeState.AnimalAbsorbState</c>) -- whether the absorbed-animal
///     value adds to the base primary stats.
/// </param>
/// <param name="AbsorbValue">
///     <c>mAnimalInfo.absorb</c> -- the absorbed-animal magnitude added to a base primary stat while
///     <paramref name="AbsorbActive" /> holds. Derived from the animal definition; the assembler leaves this 0
///     until an animal catalog lookup exists.
/// </param>
/// <param name="RuntimeAttributes">
///     <c>mAnimalInfo.attr</c> rolled runtime attribute values (<c>PlayerRuntimeState.MountRolledAttributes</c>)
///     contributing flat EFFECTIVE damage/defense adds. A <c>default</c>/empty array means "no runtime
///     attributes"; consumers must guard with <see cref="ImmutableArray{T}.IsDefaultOrEmpty" />.
/// </param>
public readonly record struct MountContext(
    int AnimalNumber = 0,
    int AnimalGrade = 0,
    bool AbsorbActive = false,
    int AbsorbValue = 0,
    ImmutableArray<int> RuntimeAttributes = default);
