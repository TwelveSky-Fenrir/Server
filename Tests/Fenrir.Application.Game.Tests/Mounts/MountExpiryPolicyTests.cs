using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountExpiryPolicyTests
{
    [Theory]
    [InlineData(10, 0, true)]  // mounted (slot 0), time run out
    [InlineData(19, 0, true)]  // mounted (slot 9), time run out
    [InlineData(10, 1, false)] // mounted but time remaining (>= 1)
    [InlineData(10, 5, false)] // mounted, time remaining
    [InlineData(9, 0, false)]  // selected-but-not-mounted, never auto-dismounts
    [InlineData(-1, 0, false)] // no mount selected
    public void IsExpiredWhileMounted_OnlyFiresInTheMountedBandWithNoTimeLeft(int animalIndex, int animalTime,
        bool expected)
    {
        Assert.Equal(expected, MountExpiryPolicy.IsExpiredWhileMounted(animalIndex, animalTime));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(19, 9)]
    public void Dismounted_LeavesTheMountedBand(int animalIndex, int expected)
    {
        Assert.Equal(expected, MountExpiryPolicy.Dismounted(animalIndex));
    }
}
