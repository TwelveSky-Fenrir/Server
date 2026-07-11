using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.World;

// world.usp_RewardBundle_GetAll / world.usp_RewardBundleItem_GetAll against real SQL Server 2025, verifying
// Database/Migrations/Seed/world/013_reward_bundle_items_legacy_correction.sql replaced 012_reward_bundles.sql's
// placeholder (ItemId=12 for all 7 slots) with the cited, confirmed-read fixed table
// ts25extra unconditionally sends to a client (Server/ts25extra/S04_MyWork02.cpp:1364-1370) -- see the D1
// wave10 legacy-behavior-translator contract this correction was written from.
[Collection("SqlServer")]
public class RewardBundleStaticDataProcTests
{
    private readonly WorldDataRepository _repository;

    public RewardBundleStaticDataProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new WorldDataRepository(db);
    }

    [Fact]
    public async Task GetRewardBundlesAsync_SeedsExactlyOneBundle()
    {
        var bundles = await _repository.GetRewardBundlesAsync(CancellationToken.None);

        var bundle = Assert.Single(bundles);
        Assert.Equal(1, bundle.RewardBundleId);
    }

    // Day-by-day item ids match Server/ts25extra/S04_MyWork02.cpp:1364-1370 verbatim, including the
    // Day1/Day6 duplicate (item 8406 appears at both SlotIndex 1 and 6) -- preserved, not "fixed", per the
    // sourcing contract's own explicit non-assertion of whether that repetition is intentional.
    [Fact]
    public async Task GetRewardBundleItemsAsync_SeedsLegacyShippedFixedTable_InSlotOrder()
    {
        var items = await _repository.GetRewardBundleItemsAsync(CancellationToken.None);

        var ordered = items.OrderBy(i => i.SlotIndex).ToList();

        Assert.Equal(7, ordered.Count);
        Assert.All(ordered, i => Assert.Equal(1, i.RewardBundleId));

        int?[] expectedBySlot1Based =
        [
            null, // index 0 unused, SlotIndex is 1-based
            8406, // Day 1: Auto Buff Scroll(7d)
            8420, // Day 2: Premium Service(1d)
            8411, // Day 3: SkyLord's Blessed Feed
            613, // Day 4: Absorption Pill(S)
            8414, // Day 5: EXP Booster(L)
            8406, // Day 6: same item id as Day 1, reproduced verbatim
            8413 // Day 7: EXP Boost [Pet]
        ];

        foreach (var row in ordered)
            Assert.Equal(expectedBySlot1Based[row.SlotIndex], row.ItemId);

        // The one duplicate this table is known to contain -- asserted explicitly so a future well-meaning
        // "dedupe the seed data" edit fails loudly instead of silently diverging from the cited source.
        Assert.Equal(ordered[0].ItemId, ordered[5].ItemId);
    }
}
