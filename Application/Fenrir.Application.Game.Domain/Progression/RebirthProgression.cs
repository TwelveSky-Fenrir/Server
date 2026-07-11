namespace Fenrir.Application.Game.Domain.Progression;

public static class RebirthProgression
{

        public const int MaxHighLevel = 12;

        public const int MaxRebirthGeneration = 12;

        public const int CombinedLevelCap = 157;

        public static readonly int[] HighLevelExpTable =
    [
        962_105_896, 1_000_590_131, 1_040_613_736, 1_082_238_285, 1_125_527_816, 1_170_548_928,
        1_217_370_885, 1_266_065_720, 1_316_708_348, 1_369_376_681, 1_424_151_748, 1_481_117_817
    ];

        public static bool IsHighLevelExperienceFull(short level2, int exp2)
    {
        return level2 == MaxHighLevel && exp2 >= HighLevelExpTable[MaxHighLevel - 1];
    }
}
