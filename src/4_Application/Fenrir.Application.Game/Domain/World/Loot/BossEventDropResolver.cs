using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Loot;

public readonly record struct BossDropOutcome(
    IReadOnlyList<DroppedItem> Items,
    IReadOnlyList<DroppedItem> PublicItems,
    int ContributionPointsGranted,
    int WarPointsGranted,
    int BloodPointsGranted,
    bool SkipGenericTiers,
    bool AnnounceEliteBossDefeat)
{
    public static readonly BossDropOutcome None = new([], [], 0, 0, 0, false, false);
}

public static class BossEventDropResolver
{
    public const int SantaMonsterId = 731;

    private const int SantaGiftItemId = 536;

    public const int NineItemEventBossMonsterId = 1404;
    private const int NineItemEventContributionPoints = 50;

    public const int DemonLordMonsterId = 287;

    private const int DemonLordKillCycle = 10;

    public const int HolyUnicornMonsterId = 576;
    private const int LabyrinthKeyItemId = 1048;
    private const int LabyrinthKeyPublicQuantity = 20;

    public const int ThreeItemEventBossMonsterId = 756;

    public const int CustomTimedBossFirstMonsterId = 564;

    public const int CustomTimedBossLastMonsterId = 568;

    public const int EliteBossMonsterId = 1407;

    public const int FifteenMinuteBossMonsterId = 1408;

    private const int FifteenMinuteBossRollCeiling = 1000;
    private const int FifteenMinuteBossTierBoundaryLow = 25;
    private const int FifteenMinuteBossTierBoundaryMid = 100;
    private const int FifteenMinuteBossTierBoundaryHigh = 400;

    private const int FifteenMinuteBossDoubleStackItemId = 1449;

    public const int VirginGhostMonsterId = 746;
    public const int SharedRandomPoolMonsterId = 9001;

    private const int VirginGhostRareItemId = 93500;

    private const int VirginGhostRareItemDropChance = 300_000;

    private const int VirginGhostWarPoints = 2;
    private const int VirginGhostBloodPoints = 2;
    private const int SharedRandomPoolContributionPoints = 5;
    private const int SharedRandomPoolWarPoints = 3;
    private const int SharedRandomPoolBloodPoints = 6;

    public static BossDropOutcome Resolve(int monsterId, int demonLordKillTally, Random random,
        WorldDataCache worldData, BossDropCatalog catalog)
    {
        return monsterId switch
        {
            SantaMonsterId => BossDropOutcome.None with { Items = [new DroppedItem(SantaGiftItemId, 1)] },

            NineItemEventBossMonsterId => BossDropOutcome.None with
            {
                Items = catalog.NineItemEventList, ContributionPointsGranted = NineItemEventContributionPoints
            },

            DemonLordMonsterId => ResolveDemonLord(demonLordKillTally, random, catalog),

            HolyUnicornMonsterId => BossDropOutcome.None with
            {
                Items = catalog.HolyUnicornPersonalList,
                PublicItems = [new DroppedItem(LabyrinthKeyItemId, LabyrinthKeyPublicQuantity)]
            },

            ThreeItemEventBossMonsterId => BossDropOutcome.None with { Items = catalog.ThreeItemEventList },

            >= CustomTimedBossFirstMonsterId and <= CustomTimedBossLastMonsterId =>
                ResolveCustomTimedBoss(monsterId, catalog),

            EliteBossMonsterId => BossDropOutcome.None with
            {
                Items = catalog.EliteBossGuaranteedList, SkipGenericTiers = true, AnnounceEliteBossDefeat = true
            },

            FifteenMinuteBossMonsterId => ResolveFifteenMinuteBoss(random, catalog),

            VirginGhostMonsterId or SharedRandomPoolMonsterId =>
                ResolveSharedRandomPool(monsterId, random, worldData, catalog),

            _ => BossDropOutcome.None
        };
    }

    private static BossDropOutcome ResolveCustomTimedBoss(int monsterId, BossDropCatalog catalog)
    {
        return catalog.CustomTimedBossLists.TryGetValue(monsterId, out var list)
            ? BossDropOutcome.None with { Items = list, SkipGenericTiers = true }
            : BossDropOutcome.None with { SkipGenericTiers = true };
    }

    private static BossDropOutcome ResolveDemonLord(int demonLordKillTally, Random random, BossDropCatalog catalog)
    {
        var pool = catalog.DemonLordItemPool;
        if (demonLordKillTally <= 0 || demonLordKillTally % DemonLordKillCycle != 0 || pool.Length == 0)
            return BossDropOutcome.None with { SkipGenericTiers = true };

        var item = pool[random.Next(pool.Length)];
        return BossDropOutcome.None with { Items = [new DroppedItem(item, 1)], SkipGenericTiers = true };
    }

    private static BossDropOutcome ResolveFifteenMinuteBoss(Random random, BossDropCatalog catalog)
    {
        var roll = random.Next(0, FifteenMinuteBossRollCeiling);

        int resolvedItemId;
        if (roll < FifteenMinuteBossTierBoundaryLow)
        {
            Span<int> pool = [BossDropHelperResolver.ResolveRandomTier2Animal(random)];
            resolvedItemId = pool[random.Next(pool.Length)];
        }
        else if (roll < FifteenMinuteBossTierBoundaryMid)
        {
            var fixedIds = catalog.FifteenMinuteBossLowMidFixedIds;
            Span<int> pool = [fixedIds[0], fixedIds[1], BossDropHelperResolver.ResolveRandomTier1Animal(random)];
            resolvedItemId = pool[random.Next(pool.Length)];
        }
        else
        {
            var pool = roll < FifteenMinuteBossTierBoundaryHigh
                ? catalog.FifteenMinuteBossMidTierPool
                : catalog.FifteenMinuteBossHighTierPool;
            resolvedItemId = pool[random.Next(pool.Length)];
        }

        if (resolvedItemId == 0)
            return BossDropOutcome.None;

        var quantity = resolvedItemId == FifteenMinuteBossDoubleStackItemId ? 2 : 1;
        return BossDropOutcome.None with { Items = [new DroppedItem(resolvedItemId, quantity)] };
    }

    private static BossDropOutcome ResolveSharedRandomPool(int monsterId, Random random, WorldDataCache worldData,
        BossDropCatalog catalog)
    {
        var isVirginGhost = monsterId == VirginGhostMonsterId;

        var contributionPoints = isVirginGhost ? 0 : SharedRandomPoolContributionPoints;
        var warPoints = isVirginGhost ? VirginGhostWarPoints : SharedRandomPoolWarPoints;
        var bloodPoints = isVirginGhost ? VirginGhostBloodPoints : SharedRandomPoolBloodPoints;

        var fixedIds = catalog.SharedRandomPoolFixedIds;
        Span<int> pool =
        [
            fixedIds[0], fixedIds[1], fixedIds[2],
            BossDropHelperResolver.ResolveRandomElixir(random),
            fixedIds[3], fixedIds[4]
        ];

        List<DroppedItem>? items = null;

        if (isVirginGhost && worldData.ItemsById.ContainsKey(VirginGhostRareItemId) &&
            LootRandomSource.RandomNumber(random) <= VirginGhostRareItemDropChance)
            (items ??= []).Add(new DroppedItem(VirginGhostRareItemId, 1));

        var resolved = pool[random.Next(pool.Length)];
        var abortGenericTiers = resolved != 0;
        if (abortGenericTiers)
            (items ??= []).Add(new DroppedItem(resolved, 1));

        return new BossDropOutcome(items ?? [], [], contributionPoints, warPoints, bloodPoints, abortGenericTiers,
            false);
    }
}
