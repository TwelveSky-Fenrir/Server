using Fenrir.Tools.LegacyDataImport.Legacy.Seeding;

namespace Fenrir.Tools.LegacyDataImport.Tests;

/// <summary>
///     End-to-end smoke tests for the actual entry point <c>Program.cs</c>'s <c>--regenerate-seed</c> flag
///     calls (<see cref="ItemSeedGenerator.Generate" />/<see cref="MonsterSeedGenerator.Generate" />):
///     confirms the corrected reader-level values (see <see cref="ItemReaderPatchTests" />/
///     <see cref="MonsterReaderPatchTests" />) actually make it into the generated SQL text, not just the
///     in-memory record.
/// </summary>
public sealed class SeedGeneratorRegressionTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(AppContext.BaseDirectory, "LegacyData");
    private readonly string _itemsOutputPath = Path.Combine(Path.GetTempPath(), $"080_items_{Guid.NewGuid():N}.sql");
    private readonly string _monstersOutputPath = Path.Combine(Path.GetTempPath(), $"090_monsters_{Guid.NewGuid():N}.sql");

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

        // Name/descriptions are plain ASCII for this item, so the whole row prefix is safe to assert as one
        // literal: ItemId, Name, Desc1, Desc2, Desc3, Type, Sort, DataNumber2D, DataNumber3D,
        // AddDataNumber3D, Level, MartialLevel, EquipInfo1, EquipInfo2, BuyCost, SellCost, BuyCost2,
        // LevelLimit, MartialLevelLimit, CheckMonsterDrop(=2).
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

        // Names are non-ASCII (mojibake Latin-1 decode) for these two -- assert only the numeric tail
        // starting right after the three (NULL) description columns, to avoid any source-encoding risk.
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

        // MonsterId, Name, ChatLine1, ChatLine2, Type, SpecialType, DamageType, DataSortNumber, Size1-4,
        // SizeCategory, CheckCollision, TotalHitNum, TotalSkillHitNum, ItemLevel, MartialItemLevel, RealLevel,
        // MartialRealLevel, GeneralExperience, PatExperience, Life, AttackType(=1).
        Assert.Contains(
            "(81, N'Thunder Giant', NULL, NULL, 1, 1, 2, 61, 19, 23, 19, 0, 2, 1, 1, 0, 133, 0, 263, 0, 400, 80, 74376, 1,",
            sql);
    }
}
