using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class Zone195TimeEventGateTests
{
    [Fact]
    public void IsOpen_IsAlwaysFalse()
    {
        Assert.False(Zone195TimeEventGate.IsOpen);
    }

    [Fact]
    public void IsOpen_DoesNotVaryWithRepeatedReads()
    {
        var first = Zone195TimeEventGate.IsOpen;
        var second = Zone195TimeEventGate.IsOpen;

        Assert.Equal(first, second);
        Assert.False(first);
    }
}
