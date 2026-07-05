using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerZoneIndexTableTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(7, 3)]
    [InlineData(8, 4)]
    [InlineData(9, 5)]
    [InlineData(12, 6)]
    [InlineData(13, 7)]
    [InlineData(14, 8)]
    [InlineData(141, 9)]
    [InlineData(142, 10)]
    [InlineData(143, 11)]
    public void KnownTowerZone_MapsToItsIndex(int zoneNumber, int expectedIndex)
    {
        Assert.Equal(expectedIndex, TowerZoneIndexTable.GetTowerIndex(zoneNumber));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(140)]
    public void UnknownZone_ReturnsNegativeOne(int zoneNumber)
    {
        Assert.Equal(-1, TowerZoneIndexTable.GetTowerIndex(zoneNumber));
    }
}
