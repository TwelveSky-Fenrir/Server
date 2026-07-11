using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Progression;

public readonly record struct TowerTribeRewardBonus(
    float SilverRatio,
    int CpForPvmBonus,
    int CpForPvpBonus,
    float XpRatio)
{

        public static readonly TowerTribeRewardBonus None = default;
}

public static class TowerRewardBonusFormulas
{
    public static float SilverBonusRatio(int builtLevel)
    {
        return builtLevel switch
        {
            1 => 0.05f,
            2 => 0.10f,
            3 => 0.15f,
            4 => 0.20f,
            _ => 0f
        };
    }

        public static int CpForPvmBonus(int builtLevel)
    {
        return builtLevel switch
        {
            1 => 1,
            2 => 2,
            3 => 2,
            4 => 2,
            _ => 0
        };
    }

        public static int CpForPvpBonus(int builtLevel)
    {
        return builtLevel switch
        {
            1 => 0,
            2 => 0,
            3 => 1,
            4 => 2,
            _ => 0
        };
    }

    public static float XpBonusRatio(int builtLevel)
    {
        return builtLevel switch
        {
            1 => 0.25f,
            2 => 0.50f,
            3 => 0.75f,
            4 => 1.00f,
            _ => 0f
        };
    }
}

public static class TowerRewardBonusTable
{
    public const int TribeCount = 4;
    public const int TowersPerTribe = 3;

        public static ImmutableArray<TowerTribeRewardBonus> Recompute(ReadOnlySpan<int> packedStateByTowerIndex)
    {
        var bonuses = new TowerTribeRewardBonus[TribeCount];

        for (var tribe = 0; tribe < TribeCount; tribe++)
        {
            var silver = 0f;
            var cpForPvm = 0;
            var cpForPvp = 0;
            var xp = 0f;

            for (var local = 0; local < TowersPerTribe; local++)
            {
                var towerIndex = tribe * TowersPerTribe + local;
                var packed = packedStateByTowerIndex[towerIndex];
                var builtLevel = TowerWarState.DecodeLevel(packed) / 2;
                if (builtLevel <= 0)
                    continue;

                switch (TowerWarState.DecodeType(packed))
                {
                    case 1:
                        silver = TowerRewardBonusFormulas.SilverBonusRatio(builtLevel);
                        break;
                    case 2:
                        cpForPvm = TowerRewardBonusFormulas.CpForPvmBonus(builtLevel);
                        cpForPvp = TowerRewardBonusFormulas.CpForPvpBonus(builtLevel);
                        break;
                    case 3:
                        xp = TowerRewardBonusFormulas.XpBonusRatio(builtLevel);
                        break;
                }
            }

            bonuses[tribe] = new TowerTribeRewardBonus(silver, cpForPvm, cpForPvp, xp);
        }

        return ImmutableArray.Create(bonuses);
    }
}
