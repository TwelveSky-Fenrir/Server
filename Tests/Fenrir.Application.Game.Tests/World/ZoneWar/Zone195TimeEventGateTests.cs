using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="Zone195TimeEventGate" />: a pure, always-<see langword="false" /> constant mirroring
///     legacy's permanently-dead <c>mGAME.isZone195TimeEvent</c> flag. There is only one behavior to pin down --
///     see the class's own remarks for the full citation trail on why it can never be anything but
///     <see langword="false" /> in this codebase today.
/// </summary>
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
        // Not a wall-clock/config-backed value -- reading it twice must never observe a different answer within
        // the same process lifetime, unlike the Sunday-20:00-21:59 computation it replaced.
        var first = Zone195TimeEventGate.IsOpen;
        var second = Zone195TimeEventGate.IsOpen;

        Assert.Equal(first, second);
        Assert.False(first);
    }
}
