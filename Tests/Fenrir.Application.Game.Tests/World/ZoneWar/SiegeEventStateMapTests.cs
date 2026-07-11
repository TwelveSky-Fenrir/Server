using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class SiegeEventStateMapTests
{
    [Theory]
    [InlineData(64, 1)]
    [InlineData(65, 2)]
    [InlineData(66, 3)]
    [InlineData(67, 4)]
    [InlineData(68, 5)]
    [InlineData(71, 6)]
    [InlineData(73, 7)]
    [InlineData(74, 8)]
    [InlineData(75, 9)]
    [InlineData(78, 10)]
    [InlineData(80, 11)]
    [InlineData(81, 12)]
    [InlineData(82, 13)]
    [InlineData(85, 14)]
    [InlineData(87, 15)]
    [InlineData(88, 16)]
    [InlineData(89, 17)]
    [InlineData(92, 18)]
    [InlineData(94, 19)]
    [InlineData(95, 20)]
    [InlineData(96, 21)]
    [InlineData(99, 22)]
    public void Zone175_DistinctValueCodes_MapToTheirOwnState(int code, int expected)
    {
        Assert.True(SiegeEventStateMap.TryMapZone175(code, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData(69)]
    [InlineData(70)]
    [InlineData(72)]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(79)]
    [InlineData(83)]
    [InlineData(84)]
    [InlineData(86)]
    [InlineData(90)]
    [InlineData(91)]
    [InlineData(93)]
    [InlineData(97)]
    [InlineData(98)]
    [InlineData(100)]
    public void Zone175_CollapsingCodes_AllMapToState23(int code)
    {
        Assert.True(SiegeEventStateMap.TryMapZone175(code, out var state));
        Assert.Equal(23, state);
    }

    [Fact]
    public void Zone175_EveryCodeIn64Through100_Maps_AndNothingElseDoes()
    {
        for (var code = 0; code < 300; code++)
        {
            var mapped = SiegeEventStateMap.TryMapZone175(code, out _);
            Assert.Equal(code is >= 64 and <= 100, mapped);
        }
    }

    [Theory]
    [InlineData(403, 1)]
    [InlineData(404, 2)]
    [InlineData(405, 3)]
    [InlineData(406, 5)]
    [InlineData(407, 5)]
    [InlineData(408, 4)]
    [InlineData(409, 5)]
    [InlineData(410, 0)]
    public void Zone267_Codes403Through410_MapToTheExactState(int code, int expected)
    {
        Assert.True(SiegeEventStateMap.TryMapZone267(code, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData(402)] // documented no-op
    [InlineData(401)]
    [InlineData(411)]
    public void Zone267_NoOpAndOutOfRangeCodes_DoNotMap(int code)
    {
        Assert.False(SiegeEventStateMap.TryMapZone267(code, out _));
    }

    [Theory]
    [InlineData(1502, 1)]
    [InlineData(1503, 2)]
    [InlineData(1504, 3)]
    [InlineData(1505, 4)]
    [InlineData(1506, 5)]
    [InlineData(1507, 0)]
    public void Zone335_Codes1502Through1507_MapToTheExactState(int code, int expected)
    {
        Assert.True(SiegeEventStateMap.TryMapZone335(code, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData(1501)] // pre-start countdown -- leaves the scalar at 0
    [InlineData(1520)] // there is no separate 1520 reset code
    [InlineData(1508)]
    public void Zone335_NoOpAndUnknownCodes_DoNotMap(int code)
    {
        Assert.False(SiegeEventStateMap.TryMapZone335(code, out _));
    }

    [Theory]
    [InlineData(411, DenOfRebirthChallengeState.ChallengeStarted)]
    [InlineData(412, DenOfRebirthChallengeState.Ended)]
    [InlineData(413, DenOfRebirthChallengeState.Ended)]
    [InlineData(414, DenOfRebirthChallengeState.Ended)]
    [InlineData(415, DenOfRebirthChallengeState.Idle)]
    public void Zone241_Codes411Through415_MapToTheThreeChallengeStates(int code, DenOfRebirthChallengeState expected)
    {
        Assert.True(SiegeEventStateMap.TryMapZone241(code, out var state));
        Assert.Equal(expected, state);
    }

    [Fact]
    public void Zone241_FailureAndSuccessCodes_CollapseToTheSameEndedState()
    {
        // 412 (fail), 413 (fail), 414 (success) all collapse -- the center-stored state cannot distinguish a
        // won challenge from a lost one.
        SiegeEventStateMap.TryMapZone241(412, out var fail1);
        SiegeEventStateMap.TryMapZone241(413, out var fail2);
        SiegeEventStateMap.TryMapZone241(414, out var success);

        Assert.Equal(DenOfRebirthChallengeState.Ended, fail1);
        Assert.Equal(fail1, fail2);
        Assert.Equal(fail1, success);
    }

    [Theory]
    [InlineData(410)]
    [InlineData(416)]
    public void Zone241_OutOfRangeCodes_DoNotMap(int code)
    {
        Assert.False(SiegeEventStateMap.TryMapZone241(code, out _));
    }
}
