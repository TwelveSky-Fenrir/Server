using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class LuckyTicketRewardResolver
{
    public const int Common = 1;

    public const int Unique = 2;

    public const int Rare = 3;

    public const int Elite = 4;

    private const int MaxItemLevel = 145;

    private const int RollCeilingExclusive = 10000;

    private const int TopBracketFloor = 9000;

    private const int MaxDrawAttempts = 3;

    public const bool ShippedProductionEliteTierEnabled = false;

    public static bool TryGetThresholds(int ticketItemId, out int firstThreshold, out int secondThreshold)
    {
        switch (ticketItemId)
        {
            case 1035:
                firstThreshold = 1;
                secondThreshold = 300;
                return true;
            case 1036:
                firstThreshold = 2;
                secondThreshold = 400;
                return true;
            case 1037:
                firstThreshold = 3;
                secondThreshold = 500;
                return true;
            default:
                firstThreshold = 0;
                secondThreshold = 0;
                return false;
        }
    }

    public static int ResolveFamilySerial(int ticketItemId)
    {
        return 100000001 + (ticketItemId - 1035);
    }

    public static (int Low, int High) ResolveItemLevelWindow(int level1, int level2)
    {
        if (level2 < 1)
        {
            var low = Math.Max(level1 - 5, 1);
            var high = Math.Min(level1 + 5, MaxItemLevel);
            return (low, high);
        }

        var fixedLevel = level1 + level2;
        return (fixedLevel, fixedLevel);
    }

    public static int ResolveTier(int roll, int level1, bool eliteTierEnabled, int firstThreshold,
        int secondThreshold)
    {
        if (roll < firstThreshold)
            return level1 switch
            {
                < 5 => Common,
                < 45 => Unique,
                < 100 => Rare,
                _ => eliteTierEnabled ? Elite : Rare
            };

        if (roll < secondThreshold)
            return level1 switch
            {
                < 5 => Common,
                < 45 => Unique,
                _ => Rare
            };

        if (roll < TopBracketFloor)
            return level1 >= 5 ? Unique : Common;

        return Common;
    }

    public static bool TryDraw(WorldDataCache worldData, Random random, int ticketItemId, byte previousTribe,
        int level1, int level2, bool eliteTierEnabled, out int rewardItemId)
    {
        if (!TryGetThresholds(ticketItemId, out var firstThreshold, out var secondThreshold))
        {
            rewardItemId = 0;
            return false;
        }

        var roll = random.Next(RollCeilingExclusive);
        var tier = ResolveTier(roll, level1, eliteTierEnabled, firstThreshold, secondThreshold);
        var (levelLow, levelHigh) = ResolveItemLevelWindow(level1, level2);
        var includeCape = tier == Rare;

        for (var attempt = 0; attempt < MaxDrawAttempts; attempt++)
            if (GeneralItemDropResolver.Resolve(worldData, random, previousTribe, tier, levelLow, levelHigh,
                    includeCape, false) is { } candidate)
            {
                rewardItemId = candidate;
                return true;
            }

        rewardItemId = 0;
        return false;
    }
}
