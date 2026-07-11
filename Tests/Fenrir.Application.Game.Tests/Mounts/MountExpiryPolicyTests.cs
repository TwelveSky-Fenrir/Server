using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountExpiryPolicyTests
{
    [Theory]
    [InlineData(10, 0, true)]
    [InlineData(19, 0, true)]
    [InlineData(10, 1, false)]
    [InlineData(10, 5, false)]
    [InlineData(9, 0, false)]
    [InlineData(-1, 0, false)]
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
