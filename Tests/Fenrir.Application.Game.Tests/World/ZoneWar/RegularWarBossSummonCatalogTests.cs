using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class RegularWarBossSummonCatalogTests
{
    [Fact]
    public void BossMonsterId_IsSixHundredSixtyOne()
    {
        Assert.Equal(561, RegularWarBossSummonCatalog.BossMonsterId);
    }

    [Fact]
    public void SummonCount_IsExactlyThree()
    {
        Assert.Equal(3, RegularWarBossSummonCatalog.SummonCount);
    }

    [Fact]
    public void SummonPosition_MatchesFixedCoordinates()
    {
        Assert.Equal(58f, RegularWarBossSummonCatalog.SummonX);
        Assert.Equal(100f, RegularWarBossSummonCatalog.SummonY);
        Assert.Equal(4190f, RegularWarBossSummonCatalog.SummonZ);
    }

    [Fact]
    public void SkipExistenceCheck_IsTrue_UnlikeBoss756()
    {
        Assert.True(RegularWarBossSummonCatalog.SkipExistenceCheck);
        Assert.NotEqual(RegularWarBossSummonCatalog.SkipExistenceCheck, Zone200GateBreachBossCatalog.ExistenceCheckActive);
    }
}
