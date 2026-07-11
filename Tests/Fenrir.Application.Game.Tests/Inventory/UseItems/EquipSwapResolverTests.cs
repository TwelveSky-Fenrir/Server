using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

public class EquipSwapResolverTests
{
    private const int IdleActionSort = 1;
    private const int CombinedLevel = 50;

    private static ItemStack Stack(int itemId, int serial = 1)
    {
        return new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, serial);
    }

    private static EquipItemValidationGate.EquipCandidate Candidate(int equipPartTag, int sort = 6,
        int tribeRestriction = EquipItemValidationGate.AnyTribeSentinel, int levelLimit = 1, int martialLevelLimit = 0,
        int checkSetItem = 0, int itemId = 1000)
    {
        return new EquipItemValidationGate.EquipCandidate(itemId, tribeRestriction, equipPartTag, levelLimit,
            martialLevelLimit, checkSetItem, sort);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(7, 5)]
    [InlineData(9, 7)]
    [InlineData(14, 12)]
    public void TryDeriveEquipSlot_MapsEachRealTagToItsSlot(byte tag, byte expectedSlot)
    {
        Assert.True(EquipSwapResolver.TryDeriveEquipSlot(tag, out var slot));
        Assert.Equal(expectedSlot, slot);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(15)]
    public void TryDeriveEquipSlot_TagWithNoRealSlot_IsRejected(byte tag)
    {
        Assert.False(EquipSwapResolver.TryDeriveEquipSlot(tag, out _));
    }

    [Fact]
    public void ClaimsItem_EquipSortWithRealTag_IsClaimed()
    {
        var item = WorldDataTestRows.Item(1000) with { Sort = 6, EquipInfo2 = 2 };
        Assert.True(EquipSwapResolver.ClaimsItem(item));
    }

    [Theory]
    [InlineData(26, 0)]
    [InlineData(5, 2)]
    [InlineData(6, 0)]
    [InlineData(34, 2)]
    public void ClaimsItem_NonEquipShapes_AreNotClaimed(byte sort, byte equipInfo2)
    {
        var item = WorldDataTestRows.Item(1000) with { Sort = sort, EquipInfo2 = equipInfo2 };
        Assert.False(EquipSwapResolver.ClaimsItem(item));
    }

    [Fact]
    public void Resolve_IntoEmptyEquipSlot_Succeeds_WithNoItemComingBack()
    {
        var used = Stack(1000, 42);

        var result = EquipSwapResolver.Resolve(used, Candidate(2),
            ImmutableDictionary<byte, ItemStack>.Empty, IdleActionSort, 1, CombinedLevel,
            0);

        Assert.True(result.Succeeded);
        Assert.Equal((byte)0, result.TargetEquipSlot);
        Assert.Equal(used, result.NewEquipStack);
        Assert.Null(result.NewInventoryStack);
    }

    [Fact]
    public void Resolve_IntoOccupiedEquipSlot_SwapsThePreviouslyEquippedPieceBack()
    {
        var used = Stack(1000, 42);
        var alreadyEquipped = Stack(2000, 7);
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, alreadyEquipped);

        var result = EquipSwapResolver.Resolve(used, Candidate(2), equipment, IdleActionSort,
            1, CombinedLevel, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(used, result.NewEquipStack);
        Assert.Equal(alreadyEquipped, result.NewInventoryStack);
    }

    [Fact]
    public void Resolve_NotIdle_IsRejectedBeforeAnythingElse()
    {
        var result = EquipSwapResolver.Resolve(Stack(1000), Candidate(2),
            ImmutableDictionary<byte, ItemStack>.Empty, 2, 1, CombinedLevel,
            0);

        Assert.Equal(EquipSwapResolver.Outcome.NotIdle, result.Outcome);
    }

    [Fact]
    public void Resolve_ItemFailsEligibilityGate_IsNotEquippable()
    {
        var result = EquipSwapResolver.Resolve(Stack(1000), Candidate(2, levelLimit: 999),
            ImmutableDictionary<byte, ItemStack>.Empty, IdleActionSort, 1, CombinedLevel,
            0);

        Assert.Equal(EquipSwapResolver.Outcome.NotEquippable, result.Outcome);
    }

    [Fact]
    public void Resolve_NullCandidate_IsNotEquippable()
    {
        var result = EquipSwapResolver.Resolve(Stack(1000), null, ImmutableDictionary<byte, ItemStack>.Empty,
            IdleActionSort, 1, CombinedLevel, 0);

        Assert.Equal(EquipSwapResolver.Outcome.NotEquippable, result.Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Resolve_EligibleButTagMapsToNoSlot_IsInvalidTargetSlot(int equipPartTag)
    {
        var result = EquipSwapResolver.Resolve(Stack(1000), Candidate(equipPartTag),
            ImmutableDictionary<byte, ItemStack>.Empty, IdleActionSort, 1, CombinedLevel,
            0);

        Assert.Equal(EquipSwapResolver.Outcome.InvalidTargetSlot, result.Outcome);
    }
}
