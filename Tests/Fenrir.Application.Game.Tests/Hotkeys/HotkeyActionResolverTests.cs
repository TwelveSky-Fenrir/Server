using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Tests.Hotkeys;

public class HotkeyActionResolverTests
{
    private static ItemStack Stack(int itemId, int quantity)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }


    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(-1, false)]
    [InlineData(3, false)]
    public void IsValidPage_RespectsPageCount(int page, bool expected)
    {
        Assert.Equal(expected, HotkeyActionResolver.IsValidPage(page));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(13, true)]
    [InlineData(-1, false)]
    [InlineData(14, false)]
    public void IsValidIndex_RespectsSlotsPerPage(int index, bool expected)
    {
        Assert.Equal(expected, HotkeyActionResolver.IsValidIndex(index));
    }


    [Fact]
    public void BindSkill_EmptyDestination_ValidGrade_Succeeds()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(5, new LearnedSkill(1001, 3));

        var result = HotkeyActionResolver.ResolveBindSkill(HotkeySlot.Empty, 0, 0, 5, 2, learned);

        Assert.True(result.Success);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Skill, 1001, 2), result.NewDestination);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    public void BindSkill_InvalidDestinationPage_Fails(int page, int index)
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(5, new LearnedSkill(1001, 3));

        var result = HotkeyActionResolver.ResolveBindSkill(HotkeySlot.Empty, page, index, 5, 2, learned);

        Assert.False(result.Success);
        Assert.Equal(HotkeyActionResolver.BindSkillFailure.InvalidDestinationPage, result.Failure);
    }

    [Fact]
    public void BindSkill_InvalidDestinationIndex_Fails()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(5, new LearnedSkill(1001, 3));

        var result = HotkeyActionResolver.ResolveBindSkill(HotkeySlot.Empty, 0, 14, 5, 2, learned);

        Assert.Equal(HotkeyActionResolver.BindSkillFailure.InvalidDestinationIndex, result.Failure);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(40)]
    public void BindSkill_InvalidSkillSlot_Fails(int skillSlotIndex)
    {
        var result = HotkeyActionResolver.ResolveBindSkill(HotkeySlot.Empty, 0, 0, skillSlotIndex, 1,
            ImmutableDictionary<byte, LearnedSkill>.Empty);

        Assert.Equal(HotkeyActionResolver.BindSkillFailure.InvalidSkillSlot, result.Failure);
    }

    [Fact]
    public void BindSkill_UnlearnedSkillSlot_Fails()
    {
        var result = HotkeyActionResolver.ResolveBindSkill(HotkeySlot.Empty, 0, 0, 5, 1,
            ImmutableDictionary<byte, LearnedSkill>.Empty);

        Assert.Equal(HotkeyActionResolver.BindSkillFailure.SkillSlotEmpty, result.Failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void BindSkill_GradeOutOfRange_Fails(int requestedGrade)
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(5, new LearnedSkill(1001, 3));

        var result = HotkeyActionResolver.ResolveBindSkill(HotkeySlot.Empty, 0, 0, 5, requestedGrade, learned);

        Assert.Equal(HotkeyActionResolver.BindSkillFailure.InvalidGrade, result.Failure);
    }

    [Theory]
    [InlineData(HotkeyBindingKind.Skill)]
    [InlineData(HotkeyBindingKind.Emoticon)]
    [InlineData(HotkeyBindingKind.Item)]
    public void BindSkill_OccupiedDestination_AnyKind_Fails(HotkeyBindingKind occupiedKind)
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(5, new LearnedSkill(1001, 3));
        var destination = new HotkeySlot(occupiedKind, 1, 1);

        var result = HotkeyActionResolver.ResolveBindSkill(destination, 0, 0, 5, 2, learned);

        Assert.Equal(HotkeyActionResolver.BindSkillFailure.DestinationOccupied, result.Failure);
    }


    [Fact]
    public void BindEmoticon_ValidCode_Succeeds_AndGradeIsForcedToZero()
    {
        var result = HotkeyActionResolver.ResolveBindEmoticon(HotkeySlot.Empty, 1, 5, 7);

        Assert.True(result.Success);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Emoticon, 7, 0), result.NewDestination);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void BindEmoticon_CodeOutOfRange_Fails(int code)
    {
        var result = HotkeyActionResolver.ResolveBindEmoticon(HotkeySlot.Empty, 0, 0, code);

        Assert.Equal(HotkeyActionResolver.BindEmoticonFailure.InvalidCode, result.Failure);
    }

    [Fact]
    public void BindEmoticon_OccupiedDestination_Fails()
    {
        var destination = new HotkeySlot(HotkeyBindingKind.Skill, 1, 1);

        var result = HotkeyActionResolver.ResolveBindEmoticon(destination, 0, 0, 3);

        Assert.Equal(HotkeyActionResolver.BindEmoticonFailure.DestinationOccupied, result.Failure);
    }


    [Fact]
    public void Unbind_SkillBinding_Succeeds()
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Skill, 1001, 3);

        var result = HotkeyActionResolver.ResolveUnbind(slot, 0, 0);

        Assert.True(result.Success);
    }

    [Fact]
    public void Unbind_EmoticonBinding_Succeeds()
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Emoticon, 3, 0);

        var result = HotkeyActionResolver.ResolveUnbind(slot, 0, 0);

        Assert.True(result.Success);
    }

    [Fact]
    public void Unbind_AlreadyEmpty_Fails_NotIdempotent()
    {
        var result = HotkeyActionResolver.ResolveUnbind(HotkeySlot.Empty, 0, 0);

        Assert.False(result.Success);
        Assert.Equal(HotkeyActionResolver.UnbindFailure.AlreadyEmpty, result.Failure);
    }

    [Fact]
    public void Unbind_ItemBinding_Fails_MustUseWithdrawInstead()
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Item, 501, 10);

        var result = HotkeyActionResolver.ResolveUnbind(slot, 0, 0);

        Assert.Equal(HotkeyActionResolver.UnbindFailure.ItemBindingNotSupported, result.Failure);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    public void Unbind_InvalidPage_Fails(int page, int index)
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Skill, 1, 1);

        var result = HotkeyActionResolver.ResolveUnbind(slot, page, index);

        Assert.Equal(HotkeyActionResolver.UnbindFailure.InvalidPage, result.Failure);
    }


    [Fact]
    public void BindItem_EmptyDestination_ClaimsSlotOutright()
    {
        var source = Stack(501, 50);

        var result = HotkeyActionResolver.ResolveBindItem(HotkeySlot.Empty, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, 20, true, false);

        Assert.True(result.Success);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Item, 501, 20), result.NewDestination);
        Assert.Equal(30, result.RemainingSourceQuantity);
    }

    [Fact]
    public void BindItem_MatchingItemDestination_Merges()
    {
        var source = Stack(501, 50);
        var destination = new HotkeySlot(HotkeyBindingKind.Item, 501, 100);

        var result = HotkeyActionResolver.ResolveBindItem(destination, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, 20, true, false);

        Assert.True(result.Success);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Item, 501, 120), result.NewDestination);
    }

    [Fact]
    public void BindItem_MismatchedItemDestination_Fails()
    {
        var source = Stack(501, 50);
        var destination = new HotkeySlot(HotkeyBindingKind.Item, 999, 100);

        var result = HotkeyActionResolver.ResolveBindItem(destination, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, 20, true, false);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.DestinationItemMismatch, result.Failure);
    }

    [Fact]
    public void BindItem_MergeExceedsCap_Fails()
    {
        var source = Stack(501, 999);
        var destination = new HotkeySlot(HotkeyBindingKind.Item, 501, 990);

        var result = HotkeyActionResolver.ResolveBindItem(destination, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, 20, true, false);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.DestinationOverCap, result.Failure);
    }

    [Theory]
    [InlineData(HotkeyBindingKind.Skill, 5)]
    [InlineData(HotkeyBindingKind.Emoticon, 0)]
    public void BindItem_UnconditionalOverwrite_NeverAddsOntoStaleSecondValue(HotkeyBindingKind occupiedKind,
        int staleSecondValue)
    {
        var source = Stack(501, 50);
        var destination = new HotkeySlot(occupiedKind, 1001, staleSecondValue);

        var result = HotkeyActionResolver.ResolveBindItem(destination, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, 10, true, false);

        Assert.True(result.Success);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Item, 501, 10), result.NewDestination);
    }

    [Fact]
    public void BindItem_NotStackable_Fails()
    {
        var source = Stack(501, 50);

        var result = HotkeyActionResolver.ResolveBindItem(HotkeySlot.Empty, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, 20, false, false);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.NotStackable, result.Failure);
    }

    [Fact]
    public void BindItem_ExcludedPotionSubtype_Fails()
    {
        var source = Stack(501, 50);

        var result = HotkeyActionResolver.ResolveBindItem(HotkeySlot.Empty, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, 20, true, true);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.ExcludedPotionSubtype, result.Failure);
    }

    [Fact]
    public void BindItem_SourceEmpty_Fails()
    {
        var result = HotkeyActionResolver.ResolveBindItem(HotkeySlot.Empty, 0, 0, null,
            ContainerMatrix.InventoryPage0, 10, 20, true, false);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.SourceEmpty, result.Failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void BindItem_QuantityOutOfRange_Fails(int quantity)
    {
        var source = Stack(501, 999);

        var result = HotkeyActionResolver.ResolveBindItem(HotkeySlot.Empty, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, quantity, true, false);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.InvalidQuantity, result.Failure);
    }

    [Fact]
    public void BindItem_QuantityExceedsSource_Fails()
    {
        var source = Stack(501, 5);

        var result = HotkeyActionResolver.ResolveBindItem(HotkeySlot.Empty, 0, 0, source,
            ContainerMatrix.InventoryPage0, 10, 6, true, false);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.InsufficientSourceQuantity, result.Failure);
    }

    [Fact]
    public void BindItem_InvalidSourcePage_Fails()
    {
        var source = Stack(501, 50);

        var result = HotkeyActionResolver.ResolveBindItem(HotkeySlot.Empty, 0, 0, source, 2, 10, 20, true, false);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.InvalidSourcePage, result.Failure);
    }

    [Fact]
    public void BindItem_InvalidSourceIndex_Fails()
    {
        var source = Stack(501, 50);

        var result = HotkeyActionResolver.ResolveBindItem(HotkeySlot.Empty, 0, 0, source,
            ContainerMatrix.InventoryPage0, 64, 20, true, false);

        Assert.Equal(HotkeyActionResolver.BindItemFailure.InvalidSourceIndex, result.Failure);
    }


    [Fact]
    public void WithdrawItem_EmptyDestination_ClaimsOutright()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 20, null,
            ContainerMatrix.InventoryPage0, 10, 3, 3);

        Assert.True(result.Success);
        Assert.Equal(501, result.NewDestinationItemId);
        Assert.Equal(20, result.NewDestinationQuantity);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Item, 501, 30), result.NewSource);
    }

    [Fact]
    public void WithdrawItem_FullWithdrawal_ClearsSourceSlot()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 20);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 20, null,
            ContainerMatrix.InventoryPage0, 10, 0, 0);

        Assert.True(result.Success);
        Assert.Equal(HotkeySlot.Empty, result.NewSource);
    }

    [Fact]
    public void WithdrawItem_MatchingDestination_Merges()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);
        var destination = Stack(501, 100);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 20, destination,
            ContainerMatrix.InventoryPage0, 10, 0, 0);

        Assert.True(result.Success);
        Assert.Equal(120, result.NewDestinationQuantity);
    }

    [Fact]
    public void WithdrawItem_MismatchedDestination_Fails()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);
        var destination = Stack(999, 100);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 20, destination,
            ContainerMatrix.InventoryPage0, 10, 0, 0);

        Assert.Equal(HotkeyActionResolver.WithdrawItemFailure.DestinationItemMismatch, result.Failure);
    }

    [Fact]
    public void WithdrawItem_DestinationOverCap_Fails()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 999);
        var destination = Stack(501, 990);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 20, destination,
            ContainerMatrix.InventoryPage0, 10, 0, 0);

        Assert.Equal(HotkeyActionResolver.WithdrawItemFailure.DestinationOverCap, result.Failure);
    }

    [Fact]
    public void WithdrawItem_SourceNotItem_Fails()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Skill, 1001, 3);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 1, null,
            ContainerMatrix.InventoryPage0, 10, 0, 0);

        Assert.Equal(HotkeyActionResolver.WithdrawItemFailure.SourceNotItem, result.Failure);
    }

    [Fact]
    public void WithdrawItem_SourceEmpty_Fails()
    {
        var result = HotkeyActionResolver.ResolveWithdrawItem(HotkeySlot.Empty, 0, 0, 1, null,
            ContainerMatrix.InventoryPage0, 10, 0, 0);

        Assert.Equal(HotkeyActionResolver.WithdrawItemFailure.SourceEmpty, result.Failure);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    public void WithdrawItem_SourcePageOutOfCorrectRange_Fails_DoesNotReproduceLegacyOobWindow(int page)
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, page, 0, 20, null,
            ContainerMatrix.InventoryPage0, 10, 0, 0);

        Assert.False(result.Success);
        Assert.Equal(HotkeyActionResolver.WithdrawItemFailure.InvalidSourcePage, result.Failure);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void WithdrawItem_DestinationXOutOfRange_Fails(int x)
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 20, null,
            ContainerMatrix.InventoryPage0, 10, x, 0);

        Assert.Equal(HotkeyActionResolver.WithdrawItemFailure.InvalidDestinationX, result.Failure);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void WithdrawItem_DestinationYOutOfRange_Fails(int y)
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 20, null,
            ContainerMatrix.InventoryPage0, 10, 0, y);

        Assert.Equal(HotkeyActionResolver.WithdrawItemFailure.InvalidDestinationY, result.Failure);
    }

    [Fact]
    public void WithdrawItem_QuantityExceedsSource_Fails()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 5);

        var result = HotkeyActionResolver.ResolveWithdrawItem(source, 0, 0, 6, null,
            ContainerMatrix.InventoryPage0, 10, 0, 0);

        Assert.Equal(HotkeyActionResolver.WithdrawItemFailure.InsufficientSourceQuantity, result.Failure);
    }


    [Fact]
    public void Rearrange_SkillBranch_EmptyDestination_MovesWholeBindingAndClearsSource()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Skill, 1001, 3);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, HotkeySlot.Empty, 1, 1, 0);

        Assert.True(result.Success);
        Assert.Equal(HotkeySlot.Empty, result.NewSource);
        Assert.Equal(source, result.NewDestination);
    }

    [Fact]
    public void Rearrange_EmoticonBranch_OccupiedDestination_Fails()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Emoticon, 3, 0);
        var destination = new HotkeySlot(HotkeyBindingKind.Emoticon, 5, 0);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, destination, 1, 1, 0);

        Assert.Equal(HotkeyActionResolver.RearrangeFailure.DestinationOccupied, result.Failure);
    }

    [Fact]
    public void Rearrange_ItemBranch_PartialQuantity_DebitsSourceAndCreditsDestination()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, HotkeySlot.Empty, 1, 1, 20);

        Assert.True(result.Success);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Item, 501, 30), result.NewSource);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Item, 501, 20), result.NewDestination);
    }

    [Fact]
    public void Rearrange_ItemBranch_FullQuantity_ClearsSource()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 20);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, HotkeySlot.Empty, 1, 1, 20);

        Assert.True(result.Success);
        Assert.Equal(HotkeySlot.Empty, result.NewSource);
    }

    [Theory]
    [InlineData(HotkeyBindingKind.Skill, 5)]
    [InlineData(HotkeyBindingKind.Emoticon, 0)]
    public void Rearrange_ItemBranch_UnconditionalOverwrite_NeverAddsOntoStaleSecondValue(
        HotkeyBindingKind occupiedKind, int staleSecondValue)
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);
        var destination = new HotkeySlot(occupiedKind, 1001, staleSecondValue);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, destination, 1, 1, 10);

        Assert.True(result.Success);
        Assert.Equal(new HotkeySlot(HotkeyBindingKind.Item, 501, 10), result.NewDestination);
    }

    [Fact]
    public void Rearrange_ItemBranch_MismatchedItemDestination_Fails()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 50);
        var destination = new HotkeySlot(HotkeyBindingKind.Item, 999, 10);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, destination, 1, 1, 10);

        Assert.Equal(HotkeyActionResolver.RearrangeFailure.DestinationItemMismatch, result.Failure);
    }

    [Fact]
    public void Rearrange_ItemBranch_MergeExceedsCap_Fails()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 999);
        var destination = new HotkeySlot(HotkeyBindingKind.Item, 501, 990);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, destination, 1, 1, 20);

        Assert.Equal(HotkeyActionResolver.RearrangeFailure.DestinationOverCap, result.Failure);
    }

    [Fact]
    public void Rearrange_SameSlot_IsNoOpSuccess_DoesNotEraseTheBinding()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Skill, 1001, 3);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 5, source, 0, 5, 0);

        Assert.True(result.Success);
        Assert.Equal(source, result.NewSource);
        Assert.Equal(source, result.NewDestination);
    }

    [Fact]
    public void Rearrange_SourceEmpty_Fails()
    {
        var result = HotkeyActionResolver.ResolveRearrange(HotkeySlot.Empty, 0, 0, HotkeySlot.Empty, 1, 1, 0);

        Assert.Equal(HotkeyActionResolver.RearrangeFailure.SourceEmpty, result.Failure);
    }

    [Fact]
    public void Rearrange_ItemBranch_QuantityExceedsSource_Fails()
    {
        var source = new HotkeySlot(HotkeyBindingKind.Item, 501, 5);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, HotkeySlot.Empty, 1, 1, 6);

        Assert.Equal(HotkeyActionResolver.RearrangeFailure.InsufficientSourceQuantity, result.Failure);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    public void Rearrange_InvalidSourcePage_Fails(int page, int index)
    {
        var source = new HotkeySlot(HotkeyBindingKind.Skill, 1001, 3);

        var result = HotkeyActionResolver.ResolveRearrange(source, page, index, HotkeySlot.Empty, 1, 1, 0);

        Assert.Equal(HotkeyActionResolver.RearrangeFailure.InvalidSourcePage, result.Failure);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    public void Rearrange_InvalidDestinationPage_Fails(int page, int index)
    {
        var source = new HotkeySlot(HotkeyBindingKind.Skill, 1001, 3);

        var result = HotkeyActionResolver.ResolveRearrange(source, 0, 0, HotkeySlot.Empty, page, index, 0);

        Assert.Equal(HotkeyActionResolver.RearrangeFailure.InvalidDestinationPage, result.Failure);
    }
}
