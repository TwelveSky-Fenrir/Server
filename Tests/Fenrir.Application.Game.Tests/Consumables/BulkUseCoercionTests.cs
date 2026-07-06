using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class BulkUseCoercionTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(999, true)]
    public void IsBulkRequest_OnlyValuesAboveOneCountAsBulk(int requestedValue, bool expected)
    {
        Assert.Equal(expected, BulkUseCoercion.IsBulkRequest(requestedValue));
    }

    [Fact]
    public void Coerce_BelowOne_IsRaisedToOne()
    {
        Assert.Equal(1, BulkUseCoercion.Coerce(0, 10));
        Assert.Equal(1, BulkUseCoercion.Coerce(-5, 10));
    }

    [Fact]
    public void Coerce_AboveStackQuantity_IsLoweredToStackQuantity()
    {
        Assert.Equal(5, BulkUseCoercion.Coerce(50, 5));
    }

    [Fact]
    public void Coerce_AboveGlobalCeiling_IsLoweredToCeiling()
    {
        Assert.Equal(BulkUseCoercion.MaxStackQuantity, BulkUseCoercion.Coerce(5000, 5000));
    }

    [Fact]
    public void Coerce_WithinRange_IsUnchanged()
    {
        Assert.Equal(7, BulkUseCoercion.Coerce(7, 10));
    }
}
