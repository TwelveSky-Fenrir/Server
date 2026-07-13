namespace Fenrir.Application.Game.Domain.Combat;

public static class ExperienceFormulas
{
    public const int RebirthDivisorLevelThreshold = 113;

    public const int MaxLimitLevel = 145;

    public const int CpLossAtLevelCap = 10;

    public const int MinimumLevelForDeathExperienceLoss = 10;

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

    public static int ApplyRebirthDivisor(int rawGain, int characterLevel)
    {
        if (rawGain <= 0)
            return 0;

        return characterLevel < RebirthDivisorLevelThreshold ? rawGain / 3 : rawGain / 5;
    }

    public static long ComputeDeathExperienceLoss(long currentExperience, int levelFactor1)
    {
        var loss = (long)((currentExperience - levelFactor1) * 0.05f);
        if (loss < 1) return 0;
        return loss > currentExperience ? currentExperience : loss;
    }

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
