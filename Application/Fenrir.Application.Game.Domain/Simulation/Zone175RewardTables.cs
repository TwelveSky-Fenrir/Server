using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Simulation;

public static class Zone175RewardTables
{

        public const int WaveCount = 5;

        public const int PreOpenCountStart = 10;

        public const byte FirstWaveBossSpecialType = 40;

        public const byte LastWaveBossSpecialType = 44;

        public const int TrickleCadenceSubTicks = 20;

        public const int OneMinuteLegacyTicks = SimulationClock.PlayTimeAccrualLegacyTicks;

        public const int PreOpenCountdownCadenceTicks = OneMinuteLegacyTicks;

        public const int WaveTimeoutLegacyTicks = 60 * OneMinuteLegacyTicks;

        public const int TerminalHoldLegacyTicks = 60 * OneMinuteLegacyTicks;

        private static readonly ImmutableArray<long> WaveClearBaseExperience =
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        public static long MoneyForStage(int stage)
    {
        return stage >= WaveCount ? 200_000_000L : 100_000_000L;
    }

        public static int ContributionPointsForStage(int stage)
    {
        return stage switch
        {
            1 => 20,
            2 => 20,
            3 => 50,
            4 => 100,
            5 => 200,
            _ => 0
        };
    }

        public static byte WaveBossSpecialType(int stage)
    {
        return (byte)(FirstWaveBossSpecialType + stage - 1);
    }

        public static bool IsWaveBossSpecialType(byte specialType)
    {
        return specialType is >= FirstWaveBossSpecialType and <= LastWaveBossSpecialType;
    }

        public static bool CanAdvanceToNextWave(int clearedWave, int index2)
    {
        return index2 >= clearedWave;
    }

        public static long WaveClearExperience(int rebirthTier, float experienceRatio)
    {
        if (rebirthTier <= 0 || rebirthTier >= WaveClearBaseExperience.Length)
            return 0;

        var baseExp = WaveClearBaseExperience[rebirthTier];
        if (baseExp <= 0 || experienceRatio <= 0f)
            return 0;

        return (long)(baseExp * experienceRatio);
    }
}
