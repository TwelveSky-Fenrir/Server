namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Port of MyUtil::GetDailyMissionReward (S07_MyGame03.cpp:7039-7064). A weighted pick over a small fixed
///     item pool; tribe/level are accepted but unused by the legacy body too.
/// </summary>
public static class DailyMissionRewardTable
{
    /// <summary>The 8 ANIMAL_NUM_*TIER2 ids GetRandomAnimal10 picks uniformly from.</summary>
    private static readonly int[] TierTwoMounts = [1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327];

    /// <summary>
    ///     <paramref name="random" /> supplies two independent [0,1) draws: species pick, then the 5%-bonus
    ///     roll and final pool pick.
    /// </summary>
    public static int Roll(Func<double> random)
    {
        Span<int> pool = stackalloc int[8];
        pool[0] = 1072; // Bronze Bar
        pool[1] = 1103; // Kabuk
        pool[2] = 1449; // 100 CP
        pool[3] = 1448; // 500 CP
        pool[4] = 1422; // Unseal scroll ("Eritme Tılsımı")
        pool[5] = 1145; // Mount Pill ("Binek Haptı")
        pool[6] = TierTwoMounts[Math.Clamp((int)(random() * TierTwoMounts.Length), 0, TierTwoMounts.Length - 1)];

        var count = 7;
        if (random() * 100 < 5) // 5% chance
        {
            pool[7] = 720; // gacha box
            count = 8;
        }

        return pool[Math.Clamp((int)(random() * count), 0, count - 1)];
    }
}
