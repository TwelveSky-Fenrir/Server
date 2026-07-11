namespace Fenrir.Application.Game.Domain.World.WorldState;

public static class TribePointLevelRecompute
{

        public const int Baseline = 1000;

        public const int LevelThreshold = 145;

        public const int LevelOffset = 112;

    public const int SecondaryLevelMultiplier = 3;
    public const int RebirthMultiplier = 3;

        public const byte BonusTribeId = 3;

    public const int BonusTribeBonus = 800;

        public static int[] ComputeTotals(IReadOnlyList<TribeRosterCharacterSnapshot> roster)
    {
        var totals = new int[WorldStateService.TribeCount];
        Array.Fill(totals, Baseline);

        foreach (var character in roster)
        {
            if (character.TribeId >= WorldStateService.TribeCount)
                continue;
            if (character.Level1 < LevelThreshold)
                continue;

            totals[character.TribeId] += character.Level1 - LevelOffset
                                         + character.Level2 * SecondaryLevelMultiplier
                                         + character.RebirthCount * RebirthMultiplier;
        }

        totals[BonusTribeId] += BonusTribeBonus;
        return totals;
    }
}
