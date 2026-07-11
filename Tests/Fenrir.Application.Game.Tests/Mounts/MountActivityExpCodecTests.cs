using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountActivityExpCodecTests
{
    [Fact]
    public void Pack_CombinesActivityAndExperience()
    {
        Assert.Equal(50_030000, MountActivityExpCodec.Pack(50, 30000));
    }

    [Fact]
    public void Pack_RoundTripsThroughDecode()
    {
        var packed = MountActivityExpCodec.Pack(73, 12345);

        Assert.Equal(73, MountActivityExpCodec.Activity(packed));
        Assert.Equal(12345, MountActivityExpCodec.Exp(packed));
    }

    [Fact]
    public void Pack_AtMaxima_DoesNotCollide()
    {
        var packed = MountActivityExpCodec.Pack(100, 100000);

        Assert.Equal(100, MountActivityExpCodec.Activity(packed));
        Assert.Equal(100000, MountActivityExpCodec.Exp(packed));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)] // "below 1 becomes 0" -- for whole numbers, the lower clamp bound
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(1000, 100)]
    public void ClampActivity_ClampsToZeroThroughHundred(int input, int expected)
    {
        Assert.Equal(expected, MountActivityExpCodec.ClampActivity(input));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(99999, 99999)]
    [InlineData(100000, 100000)]
    [InlineData(100001, 100000)]
    public void ClampExp_ClampsToZeroThroughMax(int input, int expected)
    {
        Assert.Equal(expected, MountActivityExpCodec.ClampExp(input));
    }

    [Fact]
    public void FeedActivity_AddsAndClampsAtHundred()
    {
        Assert.Equal(70, MountActivityExpCodec.FeedActivity(50, 20));
        Assert.Equal(100, MountActivityExpCodec.FeedActivity(90, 20));
        Assert.Equal(100, MountActivityExpCodec.FeedActivity(100, 5));
    }
}
