namespace Fenrir.Application.Game.Domain.World.Loot;

public static class MonsterDropTailResolver
{
    private const int CpGiftCard5ItemId = 691;
    private const int CpGiftCard10ItemId = 692;
    private const int CpGiftCard15ItemId = 693;
    private const int CpGiftCard20ItemId = 694;

    private const int CpGiftBaseRateZone126 = 50;

    private const int CpGiftBaseRateDefault = 25;

    private const int MaxHighLevel = 12;

    private const int Zone241ForcedEligibleMonsterIdLow = 725;

    private const int Zone241ForcedEligibleMonsterIdHigh = 730;

    private const int RebirthItemId = 1444;
    private const int RebirthChestLowTierItemId = 1378;
    private const int RebirthChestMidTierItemId = 1379;

    public static IReadOnlyList<DroppedItem> ResolveCpGiftCard(bool generalDropEligible, int monsterId,
        bool isZone241TypeShard, int killerLevel2, bool isZone126TypeShard, Random random)
    {
        var forcedEligible = isZone241TypeShard &&
                             monsterId is >= Zone241ForcedEligibleMonsterIdLow
                                 and <= Zone241ForcedEligibleMonsterIdHigh;

        if ((!generalDropEligible && !forcedEligible) || killerLevel2 <= 0)
            return [];

        var rate = isZone126TypeShard ? CpGiftBaseRateZone126 : CpGiftBaseRateDefault;
        if (killerLevel2 != MaxHighLevel)
            rate /= 2;

        if (LootRandomSource.RandomNumber(random) > rate)
            return [];

        var selection = random.Next(0, 100);
        var itemId = selection switch
        {
            < 80 => CpGiftCard5ItemId,
            < 90 => CpGiftCard10ItemId,
            < 95 => CpGiftCard15ItemId,
            _ => CpGiftCard20ItemId
        };

        return [new DroppedItem(itemId, 1)];
    }

    public static IReadOnlyList<DroppedItem> ResolveRebirthItem(int monsterId, Random random)
    {
        if (!IsLodBossMonster(monsterId))
            return [];

        var roll = random.Next(0, 100);

        List<DroppedItem>? items = null;

        if (roll < 30)
            (items ??= []).Add(new DroppedItem(RebirthItemId, 1));

        if (roll < 7)
            (items ??= []).Add(new DroppedItem(RebirthChestLowTierItemId, 1));
        else if (roll < 20)
            (items ??= []).Add(new DroppedItem(RebirthChestMidTierItemId, 1));

        return items ?? [];
    }

    private static bool IsLodBossMonster(int monsterId)
    {
        return monsterId is >= 725 and <= 730 or >= 736 and <= 738 or >= 748 and <= 750;
    }
}
