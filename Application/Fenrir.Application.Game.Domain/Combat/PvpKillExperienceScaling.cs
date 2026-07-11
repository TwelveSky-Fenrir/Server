namespace Fenrir.Application.Game.Domain.Combat;

public static class PvpKillExperienceScaling
{

        public const float PerLevelScaleStep = 0.1f;

        public const int UnfavorableLevelGapZeroThreshold = 9;

        public const int RegularWarXpMultiplier = 150;

        public static int Scale(int baseXp, int attackerCombinedLevel, int victimCombinedLevel)
    {
        if (attackerCombinedLevel is < PvpKillExperienceBaseTable.MinCombinedLevel
            or > PvpKillExperienceBaseTable.MaxCombinedLevel)
            return 0;

        if (victimCombinedLevel is < PvpKillExperienceBaseTable.MinCombinedLevel
            or > PvpKillExperienceBaseTable.MaxCombinedLevel)
            return 0;

        if (attackerCombinedLevel - victimCombinedLevel > UnfavorableLevelGapZeroThreshold)
            return 0;

        if (attackerCombinedLevel < victimCombinedLevel)
        {
            var favorableGap = victimCombinedLevel - attackerCombinedLevel;
            return (int)(baseXp + baseXp * (favorableGap * PerLevelScaleStep));
        }

        var unfavorableGap = attackerCombinedLevel - victimCombinedLevel;
        return (int)(baseXp - baseXp * (unfavorableGap * PerLevelScaleStep));
    }

        public static int ResolveZoneMultiplier(bool isRegularWarServer, int crossTribeXpRatio)
    {
        return isRegularWarServer ? RegularWarXpMultiplier : crossTribeXpRatio;
    }
}
