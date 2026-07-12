using Fenrir.Tools.DbMigrator.Legacy.Seeding;

namespace Fenrir.Tools.DbMigrator.Tests;

public sealed class SeedGeneratorRegressionTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(AppContext.BaseDirectory, "LegacyData");
    private readonly string _itemsOutputPath = Path.Combine(Path.GetTempPath(), $"005_items_{Guid.NewGuid():N}.sql");

    private readonly string _monstersOutputPath =
        Path.Combine(Path.GetTempPath(), $"015_monsters_{Guid.NewGuid():N}.sql");

    public void Dispose()
    {
        File.Delete(_itemsOutputPath);
        File.Delete(_monstersOutputPath);
    }

    [Fact]
    public void ItemSeedGenerator_EmitsTheCorrectedNoDropValueForItem611()
    {
        ItemSeedGenerator.Generate(_dataDirectory, _itemsOutputPath);
        var sql = File.ReadAllText(_itemsOutputPath);

        Assert.Contains(
            "(611, N'Rejuvenation Elixir(3%)', N'Mount will grow by 3%.', " +
            "N'Only apply when ridind that mount .', N'Right click to use.', 4, 3, 1787, 0, 0, 1, 0, 1, 1, 1, 1, 0, 1, 0, 2,",
            sql);
    }

    [Fact]
    public void ItemSeedGenerator_EmitsTheAuthoredSellCostForItems74200And74223()
    {
        ItemSeedGenerator.Generate(_dataDirectory, _itemsOutputPath);
        var sql = File.ReadAllText(_itemsOutputPath);

        Assert.Contains(
            "NULL, NULL, NULL, 3, 13, 997, 28, 0, 145, 1, 2, 9, 5044800, 1008960, 0, 145, 1, 1,", sql);
        Assert.Contains(
            "NULL, NULL, NULL, 3, 11, 1105, 0, 0, 145, 1, 4, 6, 3981975, 796395, 0, 145, 1, 1,", sql);
    }

    [Fact]
    public void MonsterSeedGenerator_EmitsTheCorrectedAttackTypeForMonster81()
    {
        MonsterSeedGenerator.Generate(_dataDirectory, _monstersOutputPath);
        var sql = File.ReadAllText(_monstersOutputPath);

        Assert.Contains(
            "(81, N'Thunder Giant', NULL, NULL, 1, 1, 2, 61, 19, 23, 19, 0, 2, 1, 1, 0, 133, 0, 263, 0, 400, 80, 74376, 1,",
            sql);
    }
}
