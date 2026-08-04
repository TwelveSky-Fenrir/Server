using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Simulation;

public readonly record struct Zone175ItemReward(int ItemId, int Quantity);

public static class Zone175RewardTables
{
    public const int WaveCount = 5;

    public const byte FirstWaveBossSpecialType = 40;

    public const byte LastWaveBossSpecialType = 44;

    private static readonly ImmutableArray<int> MainLevelExperience =
    [
        49_959, 55_443, 59_333, 62_405, 64_968, 67_180, 69_133, 70_887, 72_483, 73_949, 75_308, 76_575,
        1_594_846, 206_915, 229_625, 245_740, 258_464, 269_077, 278_236, 286_326, 293_592, 300_201,
        306_275, 311_902, 317_149, 322_070, 326_708, 331_096, 335_263, 339_233, 343_025, 346_657,
        350_142, 353_494, 356_723, 359_839, 362_851, 365_766, 368_592, 371_332, 373_996, 376_583,
        379_103, 381_556, 383_950, 4_625_509
    ];

    private static readonly ImmutableArray<int> MartialLevelExperience =
    [
        0, 4_810_529, 5_002_950, 5_203_068, 5_411_191, 2_813_819, 2_926_372, 3_043_427, 3_165_164,
        2_106_733, 2_191_002, 2_278_642, 2_369_788
    ];

    private static readonly ImmutableArray<ImmutableArray<Zone175ItemReward>> StageItems =
    [
        [],
        [
            new Zone175ItemReward(1025, 2), new Zone175ItemReward(1024, 2), new Zone175ItemReward(1022, 2),
            new Zone175ItemReward(1023, 2)
        ],
        [
            new Zone175ItemReward(1025, 3), new Zone175ItemReward(1024, 3), new Zone175ItemReward(1022, 3),
            new Zone175ItemReward(1023, 3)
        ],
        [
            new Zone175ItemReward(1025, 3), new Zone175ItemReward(1024, 4), new Zone175ItemReward(1022, 4),
            new Zone175ItemReward(1023, 4), new Zone175ItemReward(1166, 1), new Zone175ItemReward(539, 1),
            new Zone175ItemReward(1458, 1)
        ],
        [
            new Zone175ItemReward(1025, 3), new Zone175ItemReward(1024, 3), new Zone175ItemReward(1022, 4),
            new Zone175ItemReward(1023, 4), new Zone175ItemReward(1166, 3), new Zone175ItemReward(1455, 1),
            new Zone175ItemReward(539, 1),
            new Zone175ItemReward(1458, 1), new Zone175ItemReward(8409, 1), new Zone175ItemReward(1190, 1),
            new Zone175ItemReward(8437, 1), new Zone175ItemReward(1422, 1), new Zone175ItemReward(8102, 2)
        ],
        [
            new Zone175ItemReward(1025, 3), new Zone175ItemReward(1024, 3), new Zone175ItemReward(1022, 4),
            new Zone175ItemReward(1023, 4), new Zone175ItemReward(1166, 3), new Zone175ItemReward(1455, 1),
            new Zone175ItemReward(539, 1),
            new Zone175ItemReward(1458, 1), new Zone175ItemReward(8409, 1), new Zone175ItemReward(1190, 1),
            new Zone175ItemReward(8437, 1), new Zone175ItemReward(1422, 1), new Zone175ItemReward(8102, 2),
            new Zone175ItemReward(724, 1),
            new Zone175ItemReward(1437, 2)
        ]
    ];

    public static long MoneyForStage(int stage)
    {
        return stage switch
        {
            1 => 10_000_000,
            2 => 15_000_000,
            3 => 25_000_000,
            4 or 5 => 50_000_000,
            _ => 0
        };
    }

    public static int ContributionPointsForStage(int stage)
    {
        return stage switch
        {
            1 or 2 => 20,
            3 => 50,
            4 => 100,
            5 => 200,
            _ => 0
        };
    }

    public static ImmutableArray<Zone175ItemReward> ItemsForStage(int stage)
    {
        return stage is >= 1 and <= WaveCount ? StageItems[stage] : [];
    }

    public static byte WaveBossSpecialType(int stage)
    {
        return (byte)(FirstWaveBossSpecialType + stage - 1);
    }

    public static bool IsWaveBossSpecialType(byte specialType)
    {
        return specialType is >= FirstWaveBossSpecialType and <= LastWaveBossSpecialType;
    }

    public static int WaveClearExperience(int level, int martialLevel, int experienceRatio)
    {
        var baseExperience = martialLevel > 0
            ? martialLevel < MartialLevelExperience.Length ? MartialLevelExperience[martialLevel] : 0
            : level is >= 100 and <= 145
                ? MainLevelExperience[level - 100]
                : 0;

        if (baseExperience <= 0 || experienceRatio <= 0)
            return 0;

        return (int)Math.Min((long)baseExperience * experienceRatio, int.MaxValue);
    }
}
