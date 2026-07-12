using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class WarZoneEntryGateTests
{
    [Theory]
    [InlineData((short)1)]
    [InlineData((short)2)]
    [InlineData((short)37)]
    [InlineData((short)38)]
    public void ZoneOutsideTheCatalog_IsAlwaysAllowed_RegardlessOfLevelOrRebirth(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(zoneNumber, 1, 0));
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(zoneNumber, 999, 999));
    }

    [Fact]
    public void Zone164_ExactMaxLevelAndRebirthWithinBand_IsAllowed()
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(164, 157, 0));
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(164, 157, 6));
    }

    [Fact]
    public void Zone164_BelowMaxLevel_IsRejected()
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(164, 156, 0));
    }

    [Fact]
    public void Zone164_RebirthAboveSix_IsRejected()
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(164, 157, 7));
    }

    [Fact]
    public void Zone322_RebirthSeven_IsRejected()
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(322, 157, 7));
    }

    [Fact]
    public void Zone322_RebirthSix_IsAllowed()
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(322, 157, 6));
    }

    [Fact]
    public void Zone295_RebirthSeven_IsRejectedRegardlessOfLevel()
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(295, 157, 7));
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(295, 1, 7));
    }

    [Fact]
    public void Zone295_RebirthSix_IsAllowedRegardlessOfLevel()
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(295, 157, 6));
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(295, 1, 6));
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(295, 999, 0));
    }

    [Theory]
    [InlineData((short)296)]
    [InlineData((short)323)]
    public void HighRebirthTierZones_RebirthSix_IsRejected(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(zoneNumber, 157, 6));
    }

    [Theory]
    [InlineData((short)296)]
    [InlineData((short)323)]
    public void HighRebirthTierZones_RebirthSeven_IsAllowed(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(zoneNumber, 157, 7));
    }

    [Theory]
    [InlineData(144)]
    [InlineData(158)]
    public void Zone335_OutsideLevelBand_IsRejected(int combinedLevel)
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(335, combinedLevel, 0));
    }

    [Theory]
    [InlineData(145)]
    [InlineData(157)]
    public void Zone335_LevelBandEndpoints_AreInclusiveAndAllowed(int combinedLevel)
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(335, combinedLevel, 0));
    }

    [Fact]
    public void Zone335_RebirthTwelve_IsAllowed_RebirthThirteen_IsRejected()
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(335, 150, 12));
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(335, 150, 13));
    }

    [Theory]
    [InlineData((short)251)]
    [InlineData((short)258)]
    [InlineData((short)266)]
    public void OdawaCaveZones_LevelBandEndpoints_AreInclusiveAndAllowed(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(zoneNumber, 146, 0));
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(zoneNumber, 157, 12));
    }

    [Theory]
    [InlineData((short)251)]
    [InlineData((short)266)]
    public void OdawaCaveZones_OutsideLevelBand_IsRejected(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange, WarZoneEntryGate.Evaluate(zoneNumber, 145, 0));
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange, WarZoneEntryGate.Evaluate(zoneNumber, 158, 0));
    }

    [Fact]
    public void OdawaCaveZones_AnyRebirthCountWithinValidLevelBand_IsAllowed()
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(260, 150, 0));
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(260, 150, 12));
    }
}
