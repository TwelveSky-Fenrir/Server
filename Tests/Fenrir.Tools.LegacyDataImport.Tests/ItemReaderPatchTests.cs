namespace Fenrir.Tools.LegacyDataImport.Tests;

/// <summary>
///     Covers <c>ItemReader.ApplyRuntimePatches</c>'s two S15_MyShare.cpp-sourced patches: the 2021.04.10
///     "no drop" <c>CheckMonsterDrop</c> catalog fix (S15_MyShare.cpp:1192-1269) and the removal of the dead
///     <c>USE_CUSTOME_CREATE</c> sell-lock override for items 74200-74223 (S15_MyShare.cpp:463-469, never
///     compiled into any shipped build since both real configurations define <c>M33</c> unconditionally).
/// </summary>
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

    // Literal ids plus the two contiguous ranges from the switch-case block (S15_MyShare.cpp:1195-1241),
    // excluding 1072-1074 (NO_DROP_MONEY_BAR, never #define'd) and including the MG5ORIGIN-gated extension
    // (S15_MyShare.cpp:1259-1267), active in every real build per Header/Protocol/DEFINE.h:18.
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
        // Not every listed id is a populated slot in this particular data snapshot (e.g. 1989 has no row at
        // all) -- the legacy switch is still a no-op for an id with no matching record, so this is only
        // asserted for ids that actually exist here.
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
        // NO_DROP_MONEY_BAR is never #define'd anywhere under Server/, so this sub-case is dead code in
        // every real build -- ReadAll's CheckMonsterDrop must equal whatever ReadAllRaw already parsed for
        // these three ids, not a forced value.
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

        // Ground truth from the shipped-build export, Server/BuildEU33/ITEM_DUMP_CLEAN.csv:26093.
        Assert.Equal(1_008_960, item.SellCost);
        Assert.Equal(1, item.CheckNpcSell);
    }

    [Fact]
    public void SellLockRemoval_Item74223_PassesThroughAuthoredValues()
    {
        var item = fixture.Patched.Single(i => i.Index == 74223);

        // Ground truth from the shipped-build export, Server/BuildEU33/ITEM_DUMP_CLEAN.csv:26116.
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
        // Unrelated pre-existing patch (S15_MyShare.cpp:457-461) -- guards against the no-drop-fix edit
        // having accidentally reordered/removed this earlier check in ApplyRuntimePatches.
        Assert.DoesNotContain(fixture.Patched, i => i.Index is >= 89501 and < 89563 or 99001);
    }
}
