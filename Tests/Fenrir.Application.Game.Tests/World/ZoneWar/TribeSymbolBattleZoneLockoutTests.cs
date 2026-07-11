using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeSymbolBattleZoneLockoutTests
{
    [Theory]
    [InlineData((short)40)]
    [InlineData((short)41)]
    [InlineData((short)42)]
    public void BattleActive_GuardedSourceZone_IntoZone38_IsLockedOut(short sourceZoneId)
    {
        var result = TribeSymbolBattleZoneLockout.IsLockedOut(sourceZoneId, 38, true);

        Assert.True(result);
    }

    [Fact]
    public void BattleActive_SourceZoneNotOneOfTheThreeGuardedZones_IsNotLockedOut()
    {
        var result = TribeSymbolBattleZoneLockout.IsLockedOut(2, 38, true);

        Assert.False(result);
    }

    [Fact]
    public void BattleActive_DestinationNot38_IsNotLockedOut()
    {
        var result = TribeSymbolBattleZoneLockout.IsLockedOut(40, 50, true);

        Assert.False(result);
    }

    [Theory]
    [InlineData((short)40)]
    [InlineData((short)41)]
    [InlineData((short)42)]
    public void BattleNotActive_IsNeverLockedOut_EvenWithMatchingSourceAndDestination(short sourceZoneId)
    {
        var result = TribeSymbolBattleZoneLockout.IsLockedOut(sourceZoneId, 38, false);

        Assert.False(result);
    }

    [Fact]
    public void GuardedDestinationZoneId_IsExactly38()
    {
        Assert.Equal((short)38, TribeSymbolBattleZoneLockout.GuardedDestinationZoneId);
    }

    [Fact]
    public void GuardedSourceZoneIds_AreExactlyFortyFortyOneFortyTwo()
    {
        Assert.Equal(new HashSet<short> { 40, 41, 42 }, TribeSymbolBattleZoneLockout.GuardedSourceZoneIds);
    }
}
