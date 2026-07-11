namespace Fenrir.Tools.LegacyDataImport.Tests;

public sealed class ItemReaderPatchTests(ItemReaderFixture fixture) : IClassFixture<ItemReaderFixture>
{
    [Theory]
    [InlineData(611)]
    [InlineData(612)]
    public void NoDropFix_ForcesCheckMonsterDropToTwo(int itemId)
    {
        var item = fixture.Patched.Single(i => i.Index == itemId);

        Assert.Equal(2, item.CheckMonsterDrop);
    }

    [Theory]
    [InlineData(706)]
    [InlineData(708)]
    [InlineData(709)]
    [InlineData(710)]
    [InlineData(711)]
    [InlineData(865)]
    [InlineData(870)]
    [InlineData(885)]
    [InlineData(983)]
    [InlineData(1079)]
    [InlineData(1085)]
    [InlineData(1091)]
    [InlineData(1125)]
    [InlineData(1369)]
    [InlineData(1989)]
    [InlineData(2001)]
    [InlineData(2002)]
    [InlineData(2003)]
    [InlineData(2004)]
    [InlineData(7001)]
    [InlineData(7015)]
    [InlineData(7027)]
    [InlineData(17001)]
    [InlineData(17067)]
    [InlineData(17133)]
    public void NoDropFix_ForcesCheckMonsterDropToOne(int itemId)
    {
        var item = fixture.Patched.SingleOrDefault(i => i.Index == itemId);
        if (item is null)
            return;

        Assert.Equal(1, item.CheckMonsterDrop);
    }

    [Theory]
    [InlineData(1072)]
    [InlineData(1073)]
    [InlineData(1074)]
    public void NoDropFix_LeavesNoDropMoneyBarDeadCodeIdentifiersUntouched(int itemId)
    {
        var raw = fixture.Raw.SingleOrDefault(i => i.Index == itemId);
        var patched = fixture.Patched.SingleOrDefault(i => i.Index == itemId);
        if (raw is null || patched is null)
            return;

        Assert.Equal(raw.CheckMonsterDrop, patched.CheckMonsterDrop);
    }

    [Fact]
    public void SellLockRemoval_Item74200_PassesThroughAuthoredValues()
    {
        var item = fixture.Patched.Single(i => i.Index == 74200);

        Assert.Equal(1_008_960, item.SellCost);
        Assert.Equal(1, item.CheckNpcSell);
    }

    [Fact]
    public void SellLockRemoval_Item74223_PassesThroughAuthoredValues()
    {
        var item = fixture.Patched.Single(i => i.Index == 74223);

        Assert.Equal(796_395, item.SellCost);
        Assert.Equal(1, item.CheckNpcSell);
    }

    [Fact]
    public void SellLockRemoval_EveryIdentifierInRangeMatchesRawParse()
    {
        var raw = fixture.Raw.Where(i => i.Index is >= 74200 and <= 74223).ToDictionary(i => i.Index);
        var patched = fixture.Patched.Where(i => i.Index is >= 74200 and <= 74223).ToDictionary(i => i.Index);

        Assert.Equal(24, raw.Count);
        Assert.Equal(24, patched.Count);
        foreach (var (index, rawItem) in raw)
        {
            var patchedItem = patched[index];
            Assert.Equal(rawItem.SellCost, patchedItem.SellCost);
            Assert.Equal(rawItem.CheckNpcSell, patchedItem.CheckNpcSell);
        }
    }

    [Fact]
    public void RetiredSlotZeroing_StillAppliesAlongsideTheNoDropFix()
    {
        Assert.DoesNotContain(fixture.Patched, i => i.Index is >= 89501 and < 89563 or 99001);
    }
}
