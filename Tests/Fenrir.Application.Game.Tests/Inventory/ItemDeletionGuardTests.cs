using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Covers <see cref="ItemDeletionGuard.IsDeletionAllowed" />'s 15-id always-protected set (legacy
///     <c>CheckDeleteItem</c>, Server/Header/function.h:2454-2517) and its 0-99999 valid-range guard.
/// </summary>
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
            Assert.Contains(id, ItemDeletionGuard.ProtectedItemTypeIds);
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
        // Out-of-range is treated identically to "protected" -- refused, never "unchecked"/"allowed by default".
        Assert.False(ItemDeletionGuard.IsDeletionAllowed(itemTypeId));
    }

    [Fact]
    public void ProtectedItemTypeIds_DoesNotGrowBeyondTheFifteenDocumentedIds()
    {
        // The contract explicitly documents ~30 further ids that were once protected in the legacy switch and
        // now survive only as commented-out case labels (no longer enforced in any real build) -- their exact
        // values are not itemized by the contract, so this guard deliberately does not attempt to enumerate or
        // reject them. This test only pins the SIZE of the always-protected set so a future edit cannot
        // silently grow it back in without a fresh, cited contract update.
        Assert.Equal(15, ItemDeletionGuard.ProtectedItemTypeIds.Count);
    }
}
