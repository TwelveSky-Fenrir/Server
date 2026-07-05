namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Pure XP-gain (monster kill) and XP-loss (death by monster) formulas -- no I/O or state dependency,
///     independently unit-testable.
/// </summary>
/// <remarks>
///     Not modeled (each defaults to "feature absent"): last-hit-solo/teacher-student/party bonuses, premium/event XP
///     multipliers, pet XP, <c>aProtectForDeath</c>, and the per-account/server loss-reduction ratios.
/// </remarks>
public static class ExperienceFormulas
{
    /// <summary><c>LV_M1</c> -- XP-gain final-divisor threshold (÷3 below, ÷5 at/above).</summary>
    public const int RebirthDivisorLevelThreshold = 113;

    /// <summary>At/above this on an MvP death, the victim loses CP instead of XP.</summary>
    public const int MaxLimitLevel = 145;

    public const int CpLossAtLevelCap = 10;

    public const int MinimumLevelForDeathExperienceLoss = 10;

    /// <summary>Levels &lt;100 pass through; 100-157 use a hand-authored post-cap lookup table, not a formula.</summary>
    public static int ReturnFixedLevel(int level)
    {
        if (level < 100)
            return level;

        return level switch
        {
            100 => 102, 101 => 105, 102 => 108, 103 => 111, 104 => 114,
            105 => 117, 106 => 120, 107 => 123, 108 => 126, 109 => 129,
            110 => 132, 111 => 135, 112 => 138, 113 => 143, 114 => 149,
            115 => 155, 116 => 161, 117 => 167, 118 => 173, 119 => 179,
            120 => 185, 121 => 191, 122 => 197, 123 => 203, 124 => 209,
            125 => 215, 126 => 221, 127 => 227, 128 => 233, 129 => 239,
            130 => 245, 131 => 251, 132 => 257, 133 => 263, 134 => 269,
            135 => 275, 136 => 281, 137 => 287, 138 => 293, 139 => 299,
            140 => 305, 141 => 311, 142 => 317, 143 => 323, 144 => 329,
            145 => 335, 146 => 355, 147 => 375, 148 => 395, 149 => 415,
            150 => 455, 151 => 495, 152 => 535, 153 => 575, 154 => 635,
            155 => 695, 156 => 755, 157 => 815,
            _ => 1
        };
    }

    /// <summary>
    ///     Raw XP before <see cref="ApplyRebirthDivisor" />: 0 past a 9-level unfavorable gap, x3 past a 20-level
    ///     favorable gap, else linear ±10%/level.
    /// </summary>
    public static int ComputeMonsterKillExperience(int killerFixedLevel, int monsterRealLevel,
        int monsterGeneralExperience)
    {
        if (monsterGeneralExperience < 1)
            return 0;

        if (killerFixedLevel - monsterRealLevel > 9)
            return 0;

        float gain;
        if (monsterRealLevel > killerFixedLevel)
        {
            var favorableGap = monsterRealLevel - killerFixedLevel;
            gain = favorableGap > 20
                ? monsterGeneralExperience * 3.0f
                : monsterGeneralExperience * (1.0f + favorableGap * 0.1f);
        }
        else
        {
            var unfavorableGap = killerFixedLevel - monsterRealLevel;
            gain = monsterGeneralExperience * (1.0f - unfavorableGap * 0.1f);
            if (gain < 0f) gain = 0f;
        }

        return (int)gain;
    }

    /// <summary>Below <see cref="RebirthDivisorLevelThreshold" /> divides by 3, at/above by 5 (integer division).</summary>
    public static int ApplyRebirthDivisor(int rawGain, int characterLevel)
    {
        if (rawGain <= 0)
            return 0;

        return characterLevel < RebirthDivisorLevelThreshold ? rawGain / 3 : rawGain / 5;
    }

    /// <summary>
    ///     <c>(currentExperience - levelFactor1) * 0.05</c>, clamped to [0, currentExperience].
    ///     <paramref name="levelFactor1" /> is the level's XP-range floor.
    /// </summary>
    public static long ComputeDeathExperienceLoss(long currentExperience, int levelFactor1)
    {
        var loss = (long)((currentExperience - levelFactor1) * 0.05f);
        if (loss < 1) return 0;
        return loss > currentExperience ? currentExperience : loss;
    }

    /// <summary>
    ///     Flat 10/20/30/50% bonus (party size 2-5) granted to every present member, computed straight from the raw
    ///     monster XP -- not run through <see cref="ComputeMonsterKillExperience" /> or <see cref="ApplyRebirthDivisor" />
    ///     again.
    /// </summary>
    public static int ComputePartyBonusExperience(int presentPartySize, int monsterGeneralExperience)
    {
        if (monsterGeneralExperience < 1)
            return 0;

        var ratio = presentPartySize switch
        {
            2 => 0.1f,
            3 => 0.2f,
            4 => 0.3f,
            5 => 0.5f,
            _ => 0f
        };

        return (int)(monsterGeneralExperience * ratio);
    }
}
