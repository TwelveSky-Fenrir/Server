using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.World;

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

    [Fact]
    public async Task GetRewardBundleItemsAsync_SeedsLegacyShippedFixedTable_InSlotOrder()
    {
        var items = await _repository.GetRewardBundleItemsAsync(CancellationToken.None);

        var ordered = items.OrderBy(i => i.SlotIndex).ToList();

        Assert.Equal(7, ordered.Count);
        Assert.All(ordered, i => Assert.Equal(1, i.RewardBundleId));

        int?[] expectedBySlot1Based =
        [
            null,
            8406,
            8420,
            8411,
            613,
            8414,
            8406,
            8413
        ];

        foreach (var row in ordered)
            Assert.Equal(expectedBySlot1Based[row.SlotIndex], row.ItemId);

        Assert.Equal(ordered[0].ItemId, ordered[5].ItemId);
    }
}
