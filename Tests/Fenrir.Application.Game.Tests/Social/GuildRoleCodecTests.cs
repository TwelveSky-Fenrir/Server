using Fenrir.Application.Game.Domain.Social;

namespace Fenrir.Application.Game.Tests.Social;

public class GuildRoleCodecTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    public void DbRoleToWire_MapsExactly(byte dbRole, int expectedWire)
    {
        Assert.Equal(expectedWire, GuildRoleCodec.DbRoleToWire(dbRole));
    }

    [Fact]
    public void IsMaster_OnlyTrueForDbRoleTwo()
    {
        Assert.False(GuildRoleCodec.IsMaster(0));
        Assert.False(GuildRoleCodec.IsMaster(1));
        Assert.True(GuildRoleCodec.IsMaster(2));
    }

    [Fact]
    public void IsMasterOrSubMaster_TrueForOneAndTwoOnly()
    {
        Assert.False(GuildRoleCodec.IsMasterOrSubMaster(0));
        Assert.True(GuildRoleCodec.IsMasterOrSubMaster(1));
        Assert.True(GuildRoleCodec.IsMasterOrSubMaster(2));
    }
}
