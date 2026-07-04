namespace Fenrir.Application.Game.Combat;

/// <summary>
///     Pure XP-gain (monster kill, report 05 §5 <c>MONSTER_OBJECT::ProcessForExp</c>,
///     <c>Server/ts25zone/S07_MyGame05.cpp:3733-3920</c>) and XP-loss (player death by a monster, report 05 §4,
///     <c>ProcessAttack04</c> tail, <c>Server/ts25zone/S07_MyGame02.cpp:3445-3489</c>) formulas -- no I/O, no
///     <c>PlayerRuntimeState</c>/<c>Zone</c> dependency, so both directions are independently unit-testable.
/// </summary>
/// <remarks>
///     SCOPE cut (both directions) -- everything below needs an account/party/event subsystem
///     <see cref="Fenrir.Application.Game.World.PlayerRuntimeState" /> does not model yet, so every such term
///     defaults to its "feature absent" value (0 ratio / not premium), exactly like an un-compiled legacy
///     feature would: last-hit-solo bonus, teacher/student bonus, party bonus split, double-exp events/premium
///     (×2 stacking, up to ×32 in the source), zone-specific ratios, and the whole pet-XP side channel. The
///     death side additionally omits: <c>aProtectForDeath</c> (a consumable "skip this loss" charge) and the
///     TWO separate down-ratio multipliers (a per-account premium ratio AND a global server-config ratio,
///     both real, both currently un-modeled -- defaulted to 1.0 each, i.e. no reduction).
/// </remarks>
public static class ExperienceFormulas
{
    /// <summary><c>LV_M1</c> (DEFINE.h:451) -- the LNW33 XP-gain final-divisor threshold (report 05 §5: "÷3 avant renaissance, ÷5 après").</summary>
    public const int RebirthDivisorLevelThreshold = 113;

    /// <summary><c>MAX_LIMIT_LEVEL_NUM</c> (DEFINE.h) -- at/above this on an MvP death, the victim loses CP instead of XP (report 05 §4).</summary>
    public const int MaxLimitLevel = 145;

    /// <summary>The flat CP loss applied instead of XP loss once <see cref="MaxLimitLevel" /> is reached (S07_MyGame02.cpp:3462).</summary>
    public const int CpLossAtLevelCap = 10;

    /// <summary>The minimum character level an MvP death's XP-loss branch even applies to (S07_MyGame02.cpp:3446).</summary>
    public const int MinimumLevelForDeathExperienceLoss = 10;

    /// <summary>
    ///     <c>ReturnFixedLevel</c> (verified in full, <c>Server/Header/function.h:247-315</c>): levels &lt;100
    ///     pass through unchanged; 100-157 go through a hand-authored lookup table (NOT a formula -- the
    ///     legacy's own post-cap "high level" curve); anything else (should not occur for real character data)
    ///     falls through to the source's own literal <c>return 1;</c>.
    /// </summary>
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
    ///     Raw monster-kill XP gain BEFORE the LNW33 final divisor (<see cref="ApplyRebirthDivisor" />) --
    ///     the level-gap 3-way branch (report 05 §5): refuses (0) past a 9-level unfavorable "fixed level" gap,
    ///     triples (+ the unmodeled event ratio) past a 20-level favorable gap, else scales linearly ±10% per
    ///     level of gap. <paramref name="killerFixedLevel" /> is <see cref="ReturnFixedLevel" /> already applied
    ///     to the killer's total level (aLevel1+aLevel2 in the source; Fenrir's <c>PlayerRuntimeState</c> has no
    ///     aLevel2/rebirth-level split yet, so callers pass their plain <c>Level</c>).
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

    /// <summary>
    ///     The LNW33 final split (report 05 §5: "gain final ÷3 avant renaissance, ÷5 après") -- <paramref name="characterLevel" />
    ///     below <see cref="RebirthDivisorLevelThreshold" /> (LV_M1=113) divides by 3, at/above divides by 5.
    ///     Integer division, matching the source's own <c>int /= int</c>.
    /// </summary>
    public static int ApplyRebirthDivisor(int rawGain, int characterLevel)
    {
        if (rawGain <= 0)
            return 0;

        return characterLevel < RebirthDivisorLevelThreshold ? rawGain / 3 : rawGain / 5;
    }

    /// <summary>
    ///     XP lost on an MvP death (report 05 §4, S07_MyGame02.cpp:3466-3479): <c>(currentExperience -
    ///     levelFactor1) × 0.05</c>, clamped to <c>[0, currentExperience]</c>. <paramref name="levelFactor1" /> is
    ///     <c>ReturnLevelFactor1(level)</c> = <c>world.Levels[level].ExpRangeMin</c> (the level's own XP-range
    ///     floor) -- the caller resolves that lookup (this method takes the plain int, no catalog dependency).
    /// </summary>
    public static long ComputeDeathExperienceLoss(long currentExperience, int levelFactor1)
    {
        var loss = (long)((currentExperience - levelFactor1) * 0.05f);
        if (loss < 1) return 0;
        return loss > currentExperience ? currentExperience : loss;
    }

    /// <summary>
    ///     Party-kill bonus XP (Phase C/V6 Social, report 04's own line: "bonus 10/20/30/50% de l'XP de
    ///     base selon taille 2-5" -- verified in full against <c>MONSTER_OBJECT::ProcessForExp</c>'s own
    ///     party branch, <c>Server/ts25zone/S07_MyGame05.cpp:3899-3918</c>). A FLAT amount granted to EVERY
    ///     present party member (see <see cref="Zone.GrantMonsterKillExperience" />'s own remarks) --
    ///     computed straight from the monster's raw <paramref name="monsterGeneralExperience" />, with
    ///     NONE of <see cref="ComputeMonsterKillExperience" />'s level-gap/last-hit/event multipliers and
    ///     NOT run back through <see cref="ApplyRebirthDivisor" /> a second time (both verified: the
    ///     source computes <c>tBonusExp</c> from <c>shmMONSTER_INFO-&gt;mGeneralExperience</c> directly, in
    ///     a code path that runs AFTER the killer's own ÷3/÷5 divisor already applied to a DIFFERENT
    ///     local). <paramref name="presentPartySize" /> is the count of party members "present" (online, in
    ///     this same zone, not dead) INCLUDING the killer -- sizes outside [2,5] (can only happen if the
    ///     caller passes something other than <c>PartyRegistry</c>'s own MAX_PARTY_AVATAR_NUM=5-capped
    ///     roster) yield 0, matching the source's own <c>switch</c> having no default case.
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
