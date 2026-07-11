using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountPowerCodecTests
{
    private const int Power = 12345678;

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1, 7)]
    [InlineData(6, 2)]
    [InlineData(7, 1)]
    public void DigitAtPlace_ReadsTheDecimalDigit(int placeIndex, int expected)
    {
        Assert.Equal(expected, MountPowerCodec.DigitAtPlace(Power, placeIndex));
    }

    [Theory]
    [InlineData(1, 7)]
    [InlineData(8, 0)]
    public void AttributeIndexToPlace_MapsHighestPlaceFirst(int attributeIndex, int expectedPlace)
    {
        Assert.Equal(expectedPlace, MountPowerCodec.AttributeIndexToPlace(attributeIndex));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 8)]
    public void DigitByAttributeIndex_UsesHighestPlaceFirstMapping(int attributeIndex, int expectedDigit)
    {
        Assert.Equal(expectedDigit, MountPowerCodec.DigitByAttributeIndex(Power, attributeIndex));
    }

    [Fact]
    public void WithDigitAtPlace_ReplacesOnlyTheTargetDigit()
    {
        Assert.Equal(12345670, MountPowerCodec.WithDigitAtPlace(Power, 0, 0));
        Assert.Equal(12345679, MountPowerCodec.WithDigitAtPlace(Power, 0, 9));
        Assert.Equal(92345678, MountPowerCodec.WithDigitAtPlace(Power, 7, 9));
    }

    [Fact]
    public void DigitSum_TotalsAllEightDigits()
    {
        Assert.Equal(36, MountPowerCodec.DigitSum(Power));
        Assert.Equal(0, MountPowerCodec.DigitSum(0));
        Assert.Equal(72, MountPowerCodec.DigitSum(99999999));
    }
}
