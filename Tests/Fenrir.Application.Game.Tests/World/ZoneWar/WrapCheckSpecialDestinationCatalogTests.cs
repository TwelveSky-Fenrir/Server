using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

// Server/ts25zone/S07_MyGame03.cpp:5840-5941 -- the three non-tribe-corridor WrapCheck destination groups.
public class WrapCheckSpecialDestinationCatalogTests
{
    [Theory]
    [InlineData((short)39)]
    [InlineData((short)74)]
    [InlineData((short)144)]
    [InlineData((short)145)]
    [InlineData((short)313)]
    public void IsWinZone038Destination_TrueForEachOfTheFiveDestinations(short zoneId)
    {
        Assert.True(WrapCheckSpecialDestinationCatalog.IsWinZone038Destination(zoneId));
    }

    [Theory]
    [InlineData((short)38)] // the hub itself is not a member of this group
    [InlineData((short)1)]
    [InlineData((short)241)]
    public void IsWinZone038Destination_FalseForEverythingElse(short zoneId)
    {
        Assert.False(WrapCheckSpecialDestinationCatalog.IsWinZone038Destination(zoneId));
    }

    [Theory]
    [InlineData((short)241, 1)]
    [InlineData((short)242, 2)]
    [InlineData((short)243, 3)]
    [InlineData((short)244, 4)]
    [InlineData((short)245, 5)]
    [InlineData((short)246, 6)]
    [InlineData((short)311, 6)]
    [InlineData((short)247, 7)]
    [InlineData((short)248, 8)]
    [InlineData((short)249, 9)]
    [InlineData((short)292, 10)]
    [InlineData((short)293, 11)]
    [InlineData((short)294, 12)]
    [InlineData((short)312, 12)]
    public void TryGetRequiredRebirthCount_ResolvesTheExactMappedValue(short zoneId, int expectedRebirth)
    {
        Assert.True(WrapCheckSpecialDestinationCatalog.TryGetRequiredRebirthCount(zoneId, out var required));
        Assert.Equal(expectedRebirth, required);
    }

    [Fact]
    public void TryGetRequiredRebirthCount_MissesForAnUnrelatedZone()
    {
        Assert.False(WrapCheckSpecialDestinationCatalog.TryGetRequiredRebirthCount(1, out _));
    }

    [Theory]
    [InlineData((short)325)]
    [InlineData((short)326)]
    [InlineData((short)327)]
    [InlineData((short)328)]
    [InlineData((short)329)]
    [InlineData((short)330)]
    public void IsInstancedDestination_TrueForEachOfTheSixDestinations(short zoneId)
    {
        Assert.True(WrapCheckSpecialDestinationCatalog.IsInstancedDestination(zoneId));
    }

    [Fact]
    public void IsInstancedDestination_FalseForAnUnrelatedZone()
    {
        Assert.False(WrapCheckSpecialDestinationCatalog.IsInstancedDestination(331));
    }
}
