using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

// Server/ts25login/S04_MyWork02.cpp:330-356: relocate only when the logged-out zone's owning tribe differs
// from the avatar's own tribe. Still unwired in LoginTrain (blocked on the owning-tribe table -- see
// RespawnTownRelocation's own remarks) -- this test covers the pure decision function in isolation.
public class RespawnTownRelocationTests
{
    [Fact]
    public void RequiresRelocation_OwningTribeDiffersFromAvatarTribe_ReturnsTrue()
    {
        Assert.True(RespawnTownRelocation.RequiresRelocation(avatarTribe: 0, owningTribeOfLoggedOutZone: 1));
    }

    [Fact]
    public void RequiresRelocation_OwningTribeMatchesAvatarTribe_ReturnsFalse()
    {
        Assert.False(RespawnTownRelocation.RequiresRelocation(avatarTribe: 2, owningTribeOfLoggedOutZone: 2));
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
