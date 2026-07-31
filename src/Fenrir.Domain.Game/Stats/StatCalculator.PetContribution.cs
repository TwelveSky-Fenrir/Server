using System.Collections.Frozen;

namespace Fenrir.Domain.Game.Stats;

public static partial class StatCalculator
{
    private const int PetAmuletSort = 28;

    private static readonly FrozenSet<int> PetGradedAmuletExcludedIds =
        new[] { 2253, 2254, 2261, 2262, 2300, 2301 }.ToFrozenSet();


    private static readonly int[] PetIsLadderType1 = [30, 60, 90, 120, 150, 180, 210, 240, 270, 300];
    private static readonly int[] PetIsLadderType2 = [200, 400, 600, 800, 1000, 1200, 1400, 1600, 1800, 2000];
    private static readonly int[] PetIsLadderType3 = [20, 40, 60, 80, 100, 120, 140, 160, 180, 200];
    private static readonly int[] PetIsLadderType4 = [250, 500, 750, 1000, 1250, 1500, 1750, 2000, 2250, 2500];
    private static readonly int[] PetIsLadderType5 = [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000];
    private static readonly int[] PetIsLadderType6 = [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000];


    private static readonly float[] PetImLadderType1 = [30f, 60f, 90f, 120f, 150f, 180f, 210f, 240f, 270f, 300f];

    private static readonly float[] PetImLadderType2 =
        [200f, 400f, 600f, 800f, 1000f, 1200f, 1400f, 1600f, 1800f, 2000f];

    private static readonly float[] PetImLadderType3 =
        [100f, 200f, 300f, 400f, 500f, 600f, 700f, 800f, 900f, 1000f];

    private static readonly float[] PetImLadderType4 =
        [100f, 200f, 300f, 400f, 500f, 600f, 700f, 800f, 900f, 1000f];

    private static readonly float[] PetImLadderType5 = [0.3f, 0.3f, 0.9f, 1.2f, 1.5f, 1.8f, 2.1f, 2.4f, 2.7f, 3.0f];


    private static readonly FrozenDictionary<int, int> PetAmuletAttackTable = new Dictionary<int, int>
    {
        [8290] = 275,
        [76000] = 3000,
        [76001] = 3000,
        [76002] = 3000,
        [76003] = 3000,
        [76004] = 3000,
        [76005] = 3000,
        [76006] = 4000,
        [76007] = 5000
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, int> PetAmuletDefenseTable = new Dictionary<int, int>
    {
        [8290] = 550,
        [76000] = 3000,
        [76001] = 3000,
        [76002] = 3000,
        [76003] = 3000,
        [76004] = 3000,
        [76005] = 5000,
        [76006] = 7500,
        [76007] = 12500
    }.ToFrozenDictionary();

    private static readonly FrozenSet<int> PetAmuletPhoenixOverlapIds = new[] { 76005, 76006, 76007 }.ToFrozenSet();

    private static readonly FrozenDictionary<int, int> PetAmuletLifeTable = new Dictionary<int, int>
    {
        [8290] = 550,
        [76000] = 3000,
        [76001] = 3000,
        [76002] = 3000,
        [76003] = 3000,
        [76004] = 3000,
        [76005] = 5000,
        [76006] = 7500,
        [76007] = 12500
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, int> PetAmuletManaTable = new Dictionary<int, int>
    {
        [8290] = 500,
        [76000] = 3000,
        [76001] = 3000,
        [76002] = 3000,
        [76003] = 3000,
        [76004] = 3000,
        [76005] = 5000,
        [76006] = 7500,
        [76007] = 12500
    }.ToFrozenDictionary();


    public static int DecodePetIsByte(int packedValue)
    {
        return unchecked((sbyte)packedValue);
    }

    public static int DecodePetIuByte(int packedValue)
    {
        return unchecked((sbyte)(packedValue >> 8));
    }

    public static int DecodePetImByte(int packedValue)
    {
        return unchecked((sbyte)(packedValue >> 16));
    }

    public static int DecodePetIzByte(int packedValue)
    {
        return unchecked((sbyte)(packedValue >> 24));
    }

    private static bool IsGradedAmuletEligible(int petItemId, int petSort)
    {
        return petSort == PetAmuletSort && !PetGradedAmuletExcludedIds.Contains(petItemId);
    }

    public static int PetGradedIsBonus(int petItemId, int petSort, int packedValue, int statType)
    {
        if (!IsGradedAmuletEligible(petItemId, petSort))
            return 0;

        var isByte = DecodePetIsByte(packedValue);
        if (isByte / 10 != statType)
            return 0;

        var grade = isByte % 10;
        if (grade is < 0 or > 9)
            return 0;

        var ladder = statType switch
        {
            1 => PetIsLadderType1,
            2 => PetIsLadderType2,
            3 => PetIsLadderType3,
            4 => PetIsLadderType4,
            5 => PetIsLadderType5,
            6 => PetIsLadderType6,
            _ => null
        };

        return ladder is null ? 0 : ladder[grade];
    }


    public static int PetGradedIuBonus(int petItemId, int petSort, int packedValue, int statType)
    {
        if (!IsGradedAmuletEligible(petItemId, petSort))
            return 0;

        var iuByte = DecodePetIuByte(packedValue);
        if (iuByte / 10 != statType)
            return 0;

        var grade = iuByte % 10;
        return grade is < 0 or > 9 ? 0 : grade;
    }

    public static float PetGradedImBonus(int petItemId, int petSort, int packedValue, int statType)
    {
        if (!IsGradedAmuletEligible(petItemId, petSort))
            return 0f;

        var imByte = DecodePetImByte(packedValue);
        if (imByte / 10 != statType)
            return 0f;

        var grade = imByte % 10;
        if (grade is < 0 or > 9)
            return 0f;

        var ladder = statType switch
        {
            1 => PetImLadderType1,
            2 => PetImLadderType2,
            3 => PetImLadderType3,
            4 => PetImLadderType4,
            5 => PetImLadderType5,
            _ => null
        };

        return ladder is null ? 0f : ladder[grade];
    }

    public static int PetAmuletAttackBonus(int petItemId, int petSort)
    {
        return petSort == PetAmuletSort && PetAmuletAttackTable.TryGetValue(petItemId, out var value) ? value : 0;
    }

    public static int PetAmuletDefenseBonus(int petItemId, int petSort)
    {
        return petSort == PetAmuletSort && PetAmuletDefenseTable.TryGetValue(petItemId, out var value) ? value : 0;
    }

    public static int PetAmuletLifeBonus(int petItemId, int petSort)
    {
        return petSort == PetAmuletSort && PetAmuletLifeTable.TryGetValue(petItemId, out var value) ? value : 0;
    }

    public static int PetAmuletManaBonus(int petItemId, int petSort)
    {
        return petSort == PetAmuletSort && PetAmuletManaTable.TryGetValue(petItemId, out var value) ? value : 0;
    }


    private static float PetGrowthPercentRaw(int growValue, int tierMax)
    {
        if (tierMax <= 0)
            return 0f;

        var reducedMax = tierMax / 100000;
        if (reducedMax <= 0)
            return 0f;

        return growValue / reducedMax / 1000f;
    }

    public static float PetGrowPercent(int growValue, int tierMax)
    {
        if (growValue < 1)
            return 0f;

        return PetGrowthPercentRaw(growValue, tierMax);
    }

    public static int PetSteppedAttackBonus(int growValue, int tierMax, int activity)
    {
        if (activity < 1 || tierMax <= 0)
            return 0;

        var percent = PetGrowthPercentRaw(growValue, tierMax);
        return percent switch
        {
            < 101f => 0,
            < 125f => 50,
            < 150f => 100,
            < 175f => 150,
            < 200f => 200,
            _ => 250
        };
    }


    public static int PetGrowthValueAttackGrade(int packedValue)
    {
        return DecodePetIsByte(packedValue) switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };
    }

    public static int PetGrowthValueBonusSkillGrade(int packedValue)
    {
        return DecodePetIuByte(packedValue) switch { 11 => 1, 12 => 2, 13 => 3, _ => 0 };
    }

    public static int PetBonusSkillStatType(int skillIndex)
    {
        return skillIndex switch
        {
            103 => 1,
            82 => 2,
            83 => 3,
            105 => 4,
            104 => 5,
            84 => 6,
            _ => 0
        };
    }
}
