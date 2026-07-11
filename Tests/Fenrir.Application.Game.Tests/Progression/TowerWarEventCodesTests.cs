using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerWarEventCodesTests
{
    [Theory]
    [InlineData(751)]
    [InlineData(752)]
    [InlineData(753)]
    [InlineData(754)]
    [InlineData(755)]
    [InlineData(763)]
    [InlineData(771)]
    [InlineData(774)]
    public void EveryCodeInTheBand_IsRecognizedInert(int code)
    {
        Assert.True(TowerWarEventCodes.IsRecognizedInert(code));
        Assert.Contains(code, TowerWarEventCodes.RecognizedInertCodes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(750)]
    [InlineData(775)]
    [InlineData(120)]
    public void CodesOutsideTheBand_AreNotRecognized(int code)
    {
        Assert.False(TowerWarEventCodes.IsRecognizedInert(code));
    }

    [Fact]
    public void TheBandIsExactlyThe24InclusiveCodes751Through774()
    {
        Assert.Equal(24, TowerWarEventCodes.RecognizedInertCodes.Count);
        Assert.All(TowerWarEventCodes.RecognizedInertCodes, c => Assert.InRange(c, 751, 774));
    }

    [Fact]
    public void NamedConstants_MatchTheContractCodes()
    {
        Assert.Equal(751, TowerWarEventCodes.ProvingGroundsNewWin);
        Assert.Equal(752, TowerWarEventCodes.TowerState);
        Assert.Equal(753, TowerWarEventCodes.TowerAttackState);
        Assert.Equal(754, TowerWarEventCodes.TowerFirstHit);
        Assert.Equal(755, TowerWarEventCodes.Countdown);
        Assert.Equal(756, TowerWarEventCodes.Zone319WarBandStart);
        Assert.Equal(763, TowerWarEventCodes.Zone319WarBandEnd);
        Assert.Equal(771, TowerWarEventCodes.ProvingGroundsResultBandStart);
        Assert.Equal(774, TowerWarEventCodes.ProvingGroundsResultBandEnd);
    }
}
