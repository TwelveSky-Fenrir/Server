namespace Fenrir.Application.Game.Domain.World.WorldState;

public static class FavoredTribeRankBonusLadder
{
    public const int Baseline = 1000;
    public const int FavoredTribeBonus = 4000;

        private static readonly int[] DistanceBonus = [0, 100, 200, 300];

        public static int[] ComputeTotals(byte favoredTribeId)
    {
        if (favoredTribeId >= WorldStateService.TribeCount)
            throw new ArgumentOutOfRangeException(nameof(favoredTribeId), favoredTribeId,
                $"TribeId must be 0-{WorldStateService.TribeCount - 1}.");

        var totals = new int[WorldStateService.TribeCount];
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            var distance = (tribeId - favoredTribeId + WorldStateService.TribeCount) % WorldStateService.TribeCount;
            totals[tribeId] = Baseline + DistanceBonus[distance];
        }

        totals[favoredTribeId] += FavoredTribeBonus;
        return totals;
    }
}
