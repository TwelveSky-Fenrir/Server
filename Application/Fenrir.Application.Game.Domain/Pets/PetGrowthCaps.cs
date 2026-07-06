namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     <c>PETSYSTEM</c>'s <c>mMaxRangeValue[0..7]</c> (GameSystem_07_Pet.cpp:6-16, the <c>PETSYSTEM</c>
///     constructor) -- the single shared array both <see cref="PetExperienceCreditCalculator" /> (indices
///     0-7) and <see cref="PetGrowthTierCalculator" /> (indices 0-3 only) read from, exactly as the legacy
///     object holds one physical array both routines index into via two different, disagreeing item-id ->
///     index tables. See <see cref="PetGrowthTierCalculator" />'s remarks for the resulting discrepancy.
/// </summary>
public static class PetGrowthCaps
{
    public static readonly IReadOnlyList<int> Values =
    [
        40_000_000, 80_000_000, 160_000_000, 320_000_000,
        80_000_000, 160_000_000, 320_000_000, 640_000_000
    ];
}
