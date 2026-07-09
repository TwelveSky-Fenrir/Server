using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="TribeSymbolBattleZoneLockout" /> (<c>S04_MyWork02.cpp:2125-2141</c>) in isolation --
///     no prior test file exercised this pure decision rule directly. Wiring into
///     <see cref="Fenrir.Application.Game.Services.ZoneLifecycle.ZoneMoveService" /> (ordering relative to the
///     other zone-move gates) is covered separately in <c>ZoneMoveServiceTests</c>.
/// </summary>
public class TribeSymbolBattleZoneLockoutTests
{
    [Theory]
    [InlineData((short)40)]
    [InlineData((short)41)]
    [InlineData((short)42)]
    public void BattleActive_GuardedSourceZone_IntoZone38_IsLockedOut(short sourceZoneId)
    {
        var result = TribeSymbolBattleZoneLockout.IsLockedOut(sourceZoneId, 38, tribeSymbolBattleActive: true);

        Assert.True(result);
    }

    [Fact]
    public void BattleActive_SourceZoneNotOneOfTheThreeGuardedZones_IsNotLockedOut()
    {
        var result = TribeSymbolBattleZoneLockout.IsLockedOut(2, 38, tribeSymbolBattleActive: true);

        Assert.False(result);
    }

    [Fact]
    public void BattleActive_DestinationNot38_IsNotLockedOut()
    {
        var result = TribeSymbolBattleZoneLockout.IsLockedOut(40, 50, tribeSymbolBattleActive: true);

        Assert.False(result);
    }

    [Theory]
    [InlineData((short)40)]
    [InlineData((short)41)]
    [InlineData((short)42)]
    public void BattleNotActive_IsNeverLockedOut_EvenWithMatchingSourceAndDestination(short sourceZoneId)
    {
        var result = TribeSymbolBattleZoneLockout.IsLockedOut(sourceZoneId, 38, tribeSymbolBattleActive: false);

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
