using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class ItemDeletionGuardTests
{
    [Theory]
    [InlineData(522)]
    [InlineData(523)]
    [InlineData(524)]
    [InlineData(525)]
    [InlineData(7101)]
    [InlineData(7102)]
    [InlineData(7103)]
    [InlineData(7104)]
    [InlineData(886)]
    [InlineData(99011)]
    [InlineData(99012)]
    [InlineData(99013)]
    [InlineData(99014)]
    [InlineData(99015)]
    [InlineData(99016)]
    public void IsDeletionAllowed_ProtectedItemId_ReturnsFalse(int itemTypeId)
    {
        Assert.False(ItemDeletionGuard.IsDeletionAllowed(itemTypeId));
    }

    [Fact]
    public void ProtectedItemTypeIds_ContainsExactlyTheFifteenDocumentedIds()
    {
        int[] expected =
        [
            522, 523, 524, 525,
            7101, 7102, 7103, 7104,
            886,
            99011, 99012, 99013, 99014, 99015, 99016
        ];

        Assert.Equal(15, ItemDeletionGuard.ProtectedItemTypeIds.Count);
        foreach (var id in expected)
            Assert.Contains(id, (IEnumerable<int>)ItemDeletionGuard.ProtectedItemTypeIds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(7100)]
    [InlineData(7105)]
    [InlineData(885)]
    [InlineData(887)]
    [InlineData(99010)]
    [InlineData(99017)]
    [InlineData(99999)]
    [InlineData(0)]
    public void IsDeletionAllowed_OrdinaryInRangeItemId_ReturnsTrue(int itemTypeId)
    {
        Assert.True(ItemDeletionGuard.IsDeletionAllowed(itemTypeId));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(100000)]
    [InlineData(int.MaxValue)]
    public void IsDeletionAllowed_OutOfRangeItemId_ReturnsFalse(int itemTypeId)
    {
        Assert.False(ItemDeletionGuard.IsDeletionAllowed(itemTypeId));
    }

    [Fact]
    public void ProtectedItemTypeIds_DoesNotGrowBeyondTheFifteenDocumentedIds()
    {
        Assert.Equal(15, ItemDeletionGuard.ProtectedItemTypeIds.Count);
    }
}
