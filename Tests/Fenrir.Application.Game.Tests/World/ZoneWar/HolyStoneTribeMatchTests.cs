using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class HolyStoneTribeMatchTests
{
    [Fact]
    public void NullHolderTribe_NeverMatchesAnyTribe()
    {
        Assert.False(HolyStoneTribeMatch.Matches(0, null, null));
        Assert.False(HolyStoneTribeMatch.Matches(2, null, 2));
    }

    [Fact]
    public void SameTribeAsHolder_Matches()
    {
        Assert.True(HolyStoneTribeMatch.Matches(2, 2, null));
    }

    [Fact]
    public void DeclaredAllyOfHolder_Matches()
    {
        Assert.True(HolyStoneTribeMatch.Matches(3, 2, 3));
    }

    [Fact]
    public void NeitherHolderNorAlly_DoesNotMatch()
    {
        Assert.False(HolyStoneTribeMatch.Matches(1, 2, 3));
    }
}
