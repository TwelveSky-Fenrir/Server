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
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(zoneNumber, combinedLevel: 1, rebirthCount: 0));
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(zoneNumber, combinedLevel: 999, rebirthCount: 999));
    }

    [Fact]
    public void Zone164_ExactMaxLevelAndRebirthWithinBand_IsAllowed()
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(164, combinedLevel: 157, rebirthCount: 0));
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(164, combinedLevel: 157, rebirthCount: 6));
    }

    [Fact]
    public void Zone164_BelowMaxLevel_IsRejected()
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(164, combinedLevel: 156, rebirthCount: 0));
    }

    [Fact]
    public void Zone164_RebirthAboveSix_IsRejected()
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(164, combinedLevel: 157, rebirthCount: 7));
    }

    [Theory]
    [InlineData((short)295)]
    [InlineData((short)322)]
    public void LowRebirthTierZones_RebirthSeven_IsRejected(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(zoneNumber, combinedLevel: 157, rebirthCount: 7));
    }

    [Theory]
    [InlineData((short)295)]
    [InlineData((short)322)]
    public void LowRebirthTierZones_RebirthSix_IsAllowed(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(zoneNumber, combinedLevel: 157, rebirthCount: 6));
    }

    [Theory]
    [InlineData((short)296)]
    [InlineData((short)323)]
    public void HighRebirthTierZones_RebirthSix_IsRejected(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(zoneNumber, combinedLevel: 157, rebirthCount: 6));
    }

    [Theory]
    [InlineData((short)296)]
    [InlineData((short)323)]
    public void HighRebirthTierZones_RebirthSeven_IsAllowed(short zoneNumber)
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(zoneNumber, combinedLevel: 157, rebirthCount: 7));
    }

    [Theory]
    [InlineData(144)]
    [InlineData(158)]
    public void Zone335_OutsideLevelBand_IsRejected(int combinedLevel)
    {
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(335, combinedLevel, rebirthCount: 0));
    }

    [Theory]
    [InlineData(145)]
    [InlineData(157)]
    public void Zone335_LevelBandEndpoints_AreInclusiveAndAllowed(int combinedLevel)
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed,
            WarZoneEntryGate.Evaluate(335, combinedLevel, rebirthCount: 0));
    }

    [Fact]
    public void Zone335_RebirthTwelve_IsAllowed_RebirthThirteen_IsRejected()
    {
        Assert.Equal(WarZoneEntryOutcome.Allowed, WarZoneEntryGate.Evaluate(335, combinedLevel: 150, rebirthCount: 12));
        Assert.Equal(WarZoneEntryOutcome.RejectedOutOfRange,
            WarZoneEntryGate.Evaluate(335, combinedLevel: 150, rebirthCount: 13));
    }
}
