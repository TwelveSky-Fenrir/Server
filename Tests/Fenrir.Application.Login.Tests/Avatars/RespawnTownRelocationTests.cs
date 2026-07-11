using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

public class RespawnTownRelocationTests
{
    [Fact]
    public void RequiresRelocation_OwningTribeDiffersFromAvatarTribe_ReturnsTrue()
    {
        Assert.True(RespawnTownRelocation.RequiresRelocation(0, 1));
    }

    [Fact]
    public void RequiresRelocation_OwningTribeMatchesAvatarTribe_ReturnsFalse()
    {
        Assert.False(RespawnTownRelocation.RequiresRelocation(2, 2));
    }

    [Theory]
    [InlineData((byte)0, (byte)0, false)]
    [InlineData((byte)0, (byte)1, true)]
    [InlineData((byte)3, (byte)2, true)]
    [InlineData((byte)3, (byte)3, false)]
    public void RequiresRelocation_EveryTribeCombination(byte avatarTribe, byte owningTribe, bool expected)
    {
        Assert.Equal(expected, RespawnTownRelocation.RequiresRelocation(avatarTribe, owningTribe));
    }
}
