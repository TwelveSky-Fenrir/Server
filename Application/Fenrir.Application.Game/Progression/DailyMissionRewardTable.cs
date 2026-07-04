namespace Fenrir.Application.Game.Progression;

/// <summary>
///     Pure port of the ACTIVE (LNW33) <c>MyUtil::GetDailyMissionReward(tTribe, tLevel)</c>
///     (<c>Server/ts25zone/S07_MyGame03.cpp:7039-7064</c>, verified in full -- the non-LNW33 <c>#else</c>
///     branch, lines 7066-7119, is dead code for this build). A weighted pick over a small fixed item pool
///     (<paramref name="tribe" />/<paramref name="level" /> are read by the legacy signature but never
///     actually used inside the LNW33 body -- verified, not an omission here).
/// </summary>
/// <remarks>
///     The pool includes ONE random tier-2 mount item (<c>GetRandomAnimal10</c>,
///     <c>Server/ts25zone/S07_MyGame03.cpp:7356-7359</c>, table at line 7342) -- fully resolved against
///     <c>Server/Header/Protocol/DEFINE.h:157-183</c>'s <c>ANIMAL_NUM_*</c> constants, not invented.
/// </remarks>
public static class DailyMissionRewardTable
{
    /// <summary>
    ///     The 8 <c>ANIMAL_NUM_*TIER2</c> ids <c>GetRandomAnimal10</c> picks uniformly from (DEFINE.h:157-183, column
    ///     index 1 of the 8x3 <c>mAnimalInfo</c> table).
    /// </summary>
    private static readonly int[] TierTwoMounts = [1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327];

    /// <summary>
    ///     Rolls one reward item id. <paramref name="random" /> supplies two independent [0,1) draws: the
    ///     first resolves <c>GetRandomAnimal10()</c>'s species pick, the second resolves the outer
    ///     "5% chance to append item 720" roll AND the final uniform pick over the resulting pool (matching
    ///     the source's own two independent <c>rand()</c>/<c>rand_mir()</c> calls closely enough that the
    ///     exact RNG algorithm does not matter -- report 05 §13's own "distribution exactly, not the
    ///     algorithm" posture).
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
        if (random() * 100 < 5) // rand() % 100 < 5 -- 5% chance
        {
            pool[7] = 720; // gacha box
            count = 8;
        }

        return pool[Math.Clamp((int)(random() * count), 0, count - 1)];
    }
}
