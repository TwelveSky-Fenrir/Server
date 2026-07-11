using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

/// <summary>
///     A11 -- the 751-774 tournament/tower-war zone-to-center event-sort band. In the shipped legacy build every
///     one of these codes is recognized-but-inert on the hub, so this reference table's only job is to let a future
///     center-side ingestor recognize the whole band without a numeric literal at its call site.
/// </summary>
public class TowerWarEventCodesTests
{
    [Theory]
    [InlineData(751)] // Proving Grounds new win (band start)
    [InlineData(752)] // tower state
    [InlineData(753)] // tower attack state
    [InlineData(754)] // tower first hit
    [InlineData(755)] // countdown
    [InlineData(763)] // zone-319 war band end
    [InlineData(771)] // Proving Grounds result band start
    [InlineData(774)] // Proving Grounds result band end
    public void EveryCodeInTheBand_IsRecognizedInert(int code)
    {
        Assert.True(TowerWarEventCodes.IsRecognizedInert(code));
        Assert.Contains(code, TowerWarEventCodes.RecognizedInertCodes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(750)] // just below the band
    [InlineData(775)] // just above the band
    [InlineData(120)] // an unrelated live opcode
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
