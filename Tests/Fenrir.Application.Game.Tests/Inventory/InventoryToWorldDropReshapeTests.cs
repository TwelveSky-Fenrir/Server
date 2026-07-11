using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.Inventory;

public class InventoryToWorldDropReshapeTests
{
    private const int MonsterDrop = GroundItemEntity.MonsterKillDropSort;
    private const int InventoryDrop = GroundItemEntity.ManualGroundDropSort;
    private const int GmDrop = GroundItemEntity.GmCreateItemDropSort;

    private const byte MoneySort = 1;
    private const byte StackableSort = 2;
    private const byte MaterialSort999 = 99;


    [Fact]
    public void Money_NonMonsterDrop_DiscardsValue_KeepsQuantity_NoDeposit()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(MoneySort, InventoryDrop, 1000, 999);

        Assert.True(r.Reshaped);
        Assert.Equal(1000, r.Quantity);
        Assert.Equal(0, r.Value);
        Assert.Equal(0, r.TribeBankDepositAmount);
    }

    [Fact]
    public void Money_MonsterDrop_NoTower_Applies15PercentReduction_AndDepositsReducedAmount()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(MoneySort, MonsterDrop, 1000, 0);

        Assert.True(r.Reshaped);
        Assert.Equal(850, r.Quantity);
        Assert.Equal(0, r.Value);
        Assert.Equal(850, r.TribeBankDepositAmount);
    }

    [Fact]
    public void Money_MonsterDrop_TowerSilverRatio_AddsBackOntoGround_ButNotOntoDeposit()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(MoneySort, MonsterDrop, 1000, 0, 0.10f);

        Assert.True(r.Reshaped);
        Assert.Equal(935, r.Quantity);
        Assert.Equal(850, r.TribeBankDepositAmount);
    }

    [Fact]
    public void Money_GmDrop_TreatedAsNonMonster_NoReductionNoDeposit()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(MoneySort, GmDrop, 1000, 0);

        Assert.Equal(1000, r.Quantity);
        Assert.Equal(0, r.TribeBankDepositAmount);
    }

    [Fact]
    public void Money_ZeroAmount_IsRejected_BelowMinimumOfOne()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(MoneySort, InventoryDrop, 0, 0);

        Assert.Equal(InventoryToWorldDropPolicy.GroundDropReshapeOutcome.RejectedQuantityRange, r.Outcome);
        Assert.False(r.Reshaped);
    }

    [Fact]
    public void Money_AboveMaxNumberSentinel_IsRejected()
    {
        var over = (int)(InventoryToWorldDropPolicy.MaxNumberSentinel + 1);
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(MoneySort, InventoryDrop, over, 0);

        Assert.Equal(InventoryToWorldDropPolicy.GroundDropReshapeOutcome.RejectedQuantityRange, r.Outcome);
    }


    [Theory]
    [InlineData(StackableSort)]
    [InlineData(MaterialSort999)]
    public void Stackable_NormalQuantity_DiscardsValue(byte sort)
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(sort, InventoryDrop, 5, 999);

        Assert.True(r.Reshaped);
        Assert.Equal(5, r.Quantity);
        Assert.Equal(0, r.Value);
    }

    [Fact]
    public void Stackable_ZeroQuantity_NonGm_BecomesOne()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(StackableSort, InventoryDrop, 0, 0);

        Assert.Equal(1, r.Quantity);
    }

    [Fact]
    public void Stackable_ZeroQuantity_GmDrop_BecomesMaxDuplication()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(StackableSort, GmDrop, 0, 0);

        Assert.Equal(GroundItemPickupPolicy.MaxStackQuantity, r.Quantity);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public void Stackable_QuantityOutsideOneToMaxDup_IsRejected(int quantity)
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(StackableSort, InventoryDrop, quantity, 0);

        Assert.Equal(InventoryToWorldDropPolicy.GroundDropReshapeOutcome.RejectedQuantityRange, r.Outcome);
    }


    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    public void PetsMountsWings_ForceBothQuantityAndValueToZero(byte sort)
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(sort, InventoryDrop, 7, 12345);

        Assert.True(r.Reshaped);
        Assert.Equal(0, r.Quantity);
        Assert.Equal(0, r.Value);
    }


    [Theory]
    [InlineData((byte)7)]
    [InlineData((byte)14)]
    [InlineData((byte)21)]
    public void Equipment_ForcesQuantityZero_ButCarriesPackedValueUnchanged(byte sort)
    {
        var packed = ItemValueCodec.Encode(12, 3, 1, 2);
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(sort, InventoryDrop, 1, packed);

        Assert.True(r.Reshaped);
        Assert.Equal(0, r.Quantity);
        Assert.Equal(packed, r.Value);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(r.Value);
        Assert.Equal(12, enchant);
        Assert.Equal(3, combine);
        Assert.Equal(1, refine);
        Assert.Equal(2, socket);
    }


    [Fact]
    public void Pat_ValidValue_PassesQuantityAndValueThrough()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(22, InventoryDrop, 4, 500);

        Assert.True(r.Reshaped);
        Assert.Equal(4, r.Quantity);
        Assert.Equal(500, r.Value);
    }

    [Fact]
    public void Pat_ValueBelowZero_IsRejected()
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(22, InventoryDrop, 0, -1);

        Assert.Equal(InventoryToWorldDropPolicy.GroundDropReshapeOutcome.RejectedPackedValueRange, r.Outcome);
    }


    [Theory]
    [InlineData((byte)23)]
    [InlineData((byte)30)]
    [InlineData((byte)33)]
    public void SortsTwentyThreeToThirtyThree_PassQuantityAndValueThroughUnchanged(byte sort)
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(sort, InventoryDrop, 9, 4242);

        Assert.True(r.Reshaped);
        Assert.Equal(9, r.Quantity);
        Assert.Equal(4242, r.Value);
    }


    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)34)]
    [InlineData((byte)200)]
    public void UnhandledSort_IsRejected(byte sort)
    {
        var r = InventoryToWorldDropPolicy.ReshapeGroundDrop(sort, InventoryDrop, 1, 0);

        Assert.Equal(InventoryToWorldDropPolicy.GroundDropReshapeOutcome.RejectedUnhandledSort, r.Outcome);
        Assert.False(r.Reshaped);
    }
}
