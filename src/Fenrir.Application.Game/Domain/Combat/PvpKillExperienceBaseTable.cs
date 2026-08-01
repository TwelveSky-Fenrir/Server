namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillExperienceBaseTable
{
    public const int MinCombinedLevel = 1;

    public const int MaxCombinedLevel = 157;

    public const int LowTierBase = 110;

    public const int HighTier1Base = 330;

    public const int HighTier2Base = 360;

    public const int HighTier3Base = 390;

    private static readonly int[] BaseByCombinedLevelIndex = BuildTable();

    private static int[] BuildTable()
    {
        var table = new int[MaxCombinedLevel];

        for (var i = 0; i < 145; i++)
            table[i] = LowTierBase;

        for (var i = 145; i < 149; i++)
            table[i] = HighTier1Base;

        for (var i = 149; i < 153; i++)
            table[i] = HighTier2Base;

        for (var i = 153; i < 157; i++)
            table[i] = HighTier3Base;

        return table;
    }

    public static int Lookup(int victimCombinedLevel)
    {
        if (victimCombinedLevel is < MinCombinedLevel or > MaxCombinedLevel)
            return 0;

        return BaseByCombinedLevelIndex[victimCombinedLevel - 1];
    }
}
