using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Pets;

public static class PetExperienceCreditCalculator
{
    // Pet id -> growth category (0-7), mirroring the ReturnExperience switch. Categories 0-3 are the older
    // pets; categories 4-7 are the newer pets (doubled caps + doubled degree). The GIFT_EVENT gift ids
    // 8202-8216 are live in the shipped ReleaseEU33 build and bucket into the same category as their
    // same-tier base pet: 8202-8205 -> 4, 8206-8211 -> 5, 8212-8215 -> 6, 8216 -> 7.
    private static readonly FrozenDictionary<int, int> CategoryByItemId = new Dictionary<int, int>
    {
        [541] = 0, [542] = 0, [547] = 0, [560] = 0,
        [543] = 1, [544] = 1, [548] = 1, [561] = 1, [1452] = 1, [86819] = 1,
        [545] = 2, [549] = 2, [562] = 2, [86820] = 2,
        [546] = 3, [550] = 3,
        [1002] = 4, [1003] = 4, [2140] = 4, [1004] = 4, [1005] = 4,
        [8202] = 4, [8203] = 4, [8204] = 4, [8205] = 4,
        [1006] = 5, [1007] = 5, [1008] = 5, [1009] = 5, [1010] = 5, [1011] = 5, [17052] = 5,
        [8206] = 5, [8207] = 5, [8208] = 5, [8209] = 5, [8210] = 5, [8211] = 5,
        [1012] = 6, [1013] = 6, [1014] = 6, [1015] = 6, [17053] = 6,
        [8212] = 6, [8213] = 6, [8214] = 6, [8215] = 6,
        [1016] = 7, [1310] = 7, [1311] = 7, [1312] = 7, [2133] = 7, [2144] = 7, [2160] = 7,
        [17055] = 7, [17056] = 7, [17057] = 7,
        [8216] = 7
    }.ToFrozenDictionary();

    public static bool TryResolveCategory(int petItemId, out int categoryIndex)
    {
        return CategoryByItemId.TryGetValue(petItemId, out categoryIndex);
    }

    /// <summary>
    ///     Growth credited for one pet feed event, matching legacy <c>PETSYSTEM::ReturnExperience</c>.
    ///     Two distinct feed paths reach this routine:
    ///     the monster-kill / experience-distribution path passes a zero <paramref name="growUpValue" /> and
    ///     credits the raw <paramref name="seedExperience" /> directly; the pet-food / GM-fill path passes a
    ///     positive <paramref name="growUpValue" /> (1 / 3 / 40 / 200), discards the seed, and credits a
    ///     per-category multiplier times that grow-up value. Both are clamped to the remaining room below the
    ///     category cap.
    /// </summary>
    public static int ComputeCreditedAmount(int petItemId, int currentGrowth, int seedExperience,
        float growUpValue = 0f)
    {
        if (!TryResolveCategory(petItemId, out var categoryIndex))
            return 0;

        var cap = PetGrowthCaps.Values[categoryIndex];
        if (currentGrowth >= cap)
            return 0;

        int rawCredit;
        if (growUpValue > 0f)
        {
            // Positive grow-up (food / GM) path: the seed is ignored; the credit is the category multiplier
            // times the grow-up value. Reproduce the legacy operation order in single-precision float to stay
            // byte-faithful (cap x100 -> / degree -> truncate -> /100 -> ceiling), then truncate the product.
            // Degree is 100 for the old pets (categories 0-3) and 200 for the new pets (categories 4-7); with
            // the doubled new-pet caps this yields the same multiplier as the same-tier old-pet category.
            var degree = categoryIndex <= 3 ? 100 : 200;
            var multiplier = MathF.Ceiling(MathF.Truncate(cap * 100f / degree) / 100f);
            rawCredit = (int)(multiplier * growUpValue);
        }
        else
        {
            // Zero / non-positive grow-up (kill / distribution) path: the seed is credited directly, no
            // category multiplier.
            rawCredit = seedExperience;
        }

        if (rawCredit <= 0)
            return 0;

        var projected = currentGrowth + rawCredit;
        return projected > cap ? cap - currentGrowth : rawCredit;
    }
}
