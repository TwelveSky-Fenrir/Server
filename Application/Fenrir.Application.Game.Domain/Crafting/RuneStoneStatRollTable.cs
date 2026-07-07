using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Crafting;

/// <summary>
///     The tiered stat-roll distribution shared by all 3 rune-stone-crafting source items: one draw from a
///     200-value range, mapped through 4 unevenly-sized tiers, clamped to a hard ceiling of 30.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:963-988.
///     <para>
///         The backing contract gives exact tier *sizes* -- 2.5% / 10% / 25% / 62.5% of the 200-value draw
///         range, i.e. 5 / 20 / 50 / 125 draws respectively (these divide 200 exactly, so "~" in the
///         contract's own wording is just informal phrasing, not genuine imprecision) -- and each tier's
///         output *value range* (26-30 / 20-24 / 15-19 / 1-14), but not the legacy's own exact intra-tier
///         draw-to-value formula. The mapping below is therefore a documented, not independently
///         source-verified, modeling choice. The top tier (5 draws/5 values) and the
///         lower-mid tier (50 draws/5 values) divide into perfectly even 1-per-value and 10-per-value
///         strides respectively. The upper-mid tier (20 draws/5 values) is the one exception: its lowest
///         value (20) absorbs one extra draw at the tier's own boundary (draws 175-179, 5 draws) while the
///         remaining 3 strides are a clean 4-per-value (180-183/184-187/188-191) and the top value (24) gets
///         only 3 draws (192-194) -- a 5/4/4/4/3 split, not 4/4/4/4/4. The bottom tier (125 draws over 14
///         values) does not divide evenly either, so its mapping is proportional
///         (<c>1 + draw * 14 / 125</c>) rather than a fixed per-value stride. Revisit if a byte-exact
///         citation of the intra-tier formula becomes available.
///     </para>
/// </remarks>
public static class RuneStoneStatRollTable
{
    public const int DrawRange = 200;
    public const int HardCeiling = 30;

    private const int TopTierDrawStart = 195;
    private const int TopTierValueMin = 26;

    private const int UpperMidTierDrawStart = 175;
    private const int UpperMidTierValueMin = 20;
    private const int UpperMidTierDrawsPerValue = 4;

    private const int LowerMidTierDrawStart = 125;
    private const int LowerMidTierValueMin = 15;
    private const int LowerMidTierDrawsPerValue = 10;

    private const int BaseTierValueMin = 1;
    private const int BaseTierValueCount = 14;

    public static int Roll(IRandomSource random)
    {
        return RollFromDraw(random.NextInt32(DrawRange));
    }

    /// <summary>Exposed so the tier boundaries can be asserted directly, without needing an IRandomSource fake.</summary>
    public static int RollFromDraw(int draw)
    {
        var value = draw switch
        {
            >= TopTierDrawStart => TopTierValueMin + (draw - TopTierDrawStart),
            // The lowest value's bucket absorbs one extra draw at the tier's own start (draws 175-179,
            // 5 draws) -- see this type's own remarks for the resulting 5/4/4/4/3 split.
            >= UpperMidTierDrawStart => UpperMidTierValueMin +
                                        Math.Max(0, draw - UpperMidTierDrawStart - 1) / UpperMidTierDrawsPerValue,
            >= LowerMidTierDrawStart => LowerMidTierValueMin +
                                        (draw - LowerMidTierDrawStart) / LowerMidTierDrawsPerValue,
            _ => BaseTierValueMin + draw * BaseTierValueCount / LowerMidTierDrawStart
        };

        return Math.Min(value, HardCeiling);
    }
}
