using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Crafting;

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

    public static int RollFromDraw(int draw)
    {
        var value = draw switch
        {
            >= TopTierDrawStart => TopTierValueMin + (draw - TopTierDrawStart),
            >= UpperMidTierDrawStart => UpperMidTierValueMin +
                                        Math.Max(0, draw - UpperMidTierDrawStart - 1) / UpperMidTierDrawsPerValue,
            >= LowerMidTierDrawStart => LowerMidTierValueMin +
                                        (draw - LowerMidTierDrawStart) / LowerMidTierDrawsPerValue,
            _ => BaseTierValueMin + draw * BaseTierValueCount / LowerMidTierDrawStart
        };

        return Math.Min(value, HardCeiling);
    }
}
