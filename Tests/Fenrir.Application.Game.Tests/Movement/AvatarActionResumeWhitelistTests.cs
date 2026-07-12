using Fenrir.Application.Game.Domain.Movement;

namespace Fenrir.Application.Game.Tests.Movement;

public class AvatarActionResumeWhitelistTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(11, 0)]
    [InlineData(19, 0)]
    [InlineData(31, 0)]
    [InlineData(32, 0)]
    [InlineData(64, 0)]
    [InlineData(90, 0)]
    [InlineData(91, 0)]
    [InlineData(92, 0)]
    [InlineData(93, 0)]
    [InlineData(94, 0)]
    [InlineData(95, 0)]
    public void IsLegal_AcceptedSortWithZeroType_ReturnsTrue(int sort, int type)
    {
        Assert.True(AvatarActionResumeWhitelist.IsLegal(sort, type));
    }

    [Theory]
    [InlineData(19, 1)]
    [InlineData(31, 1)]
    [InlineData(64, 1)]
    [InlineData(19, 8)]
    [InlineData(31, 999)]
    public void IsLegal_SortRequiringZeroType_RejectsNonzeroType(int sort, int type)
    {
        Assert.False(AvatarActionResumeWhitelist.IsLegal(sort, type));
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(2, 8)]
    [InlineData(32, 100)]
    [InlineData(90, -1)]
    [InlineData(95, 999)]
    public void IsLegal_SortWithoutTypeRestriction_AcceptsAnyType(int sort, int type)
    {
        Assert.True(AvatarActionResumeWhitelist.IsLegal(sort, type));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(65, 0)]
    [InlineData(96, 0)]
    [InlineData(-1, 0)]
    public void IsLegal_SortOutsideAcceptedSet_ReturnsFalse(int sort, int type)
    {
        Assert.False(AvatarActionResumeWhitelist.IsLegal(sort, type));
    }

    [Theory]
    [InlineData(94)]
    [InlineData(95)]
    public void ClearsFishingProgress_ResultSorts_ReturnsTrue(int sort)
    {
        Assert.True(AvatarActionResumeWhitelist.ClearsFishingProgress(sort));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(64)]
    [InlineData(92)]
    [InlineData(93)]
    [InlineData(0)]
    public void ClearsFishingProgress_EveryOtherSort_ReturnsFalse(int sort)
    {
        Assert.False(AvatarActionResumeWhitelist.ClearsFishingProgress(sort));
    }
}
