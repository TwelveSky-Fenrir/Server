namespace Fenrir.Application.Game.Domain.Progression;

public static class DailyMissionRewardTable
{

        private static readonly int[] TierTwoMounts = [1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327];

        public static int Roll(Func<double> random)
    {
        Span<int> pool = stackalloc int[8];
        pool[0] = 1072;
        pool[1] = 1103;
        pool[2] = 1449;
        pool[3] = 1448;
        pool[4] = 1422;
        pool[5] = 1145;
        pool[6] = TierTwoMounts[Math.Clamp((int)(random() * TierTwoMounts.Length), 0, TierTwoMounts.Length - 1)];

        var count = 7;
        if (random() * 100 < 5)
        {
            pool[7] = 720;
            count = 8;
        }

        return pool[Math.Clamp((int)(random() * count), 0, count - 1)];
    }
}
