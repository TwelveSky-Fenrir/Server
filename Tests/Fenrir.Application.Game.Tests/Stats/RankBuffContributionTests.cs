using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

public class RankBuffContributionTests
{
    private const short NeutralZone = 1;

    private static ZoneContext Zone(int rankBuffType, short zoneNumber = NeutralZone)
    {
        return new ZoneContext(zoneNumber, RankBuffType: rankBuffType);
    }


    [Fact]
    public void Tier1_BoostsDefensePower_By1000_AndNothingElse()
    {
        var zone = Zone(1);

        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(zone));

        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
    }

    [Fact]
    public void Tier2_BoostsElementDefensePower_By1000_AndNothingElse()
    {
        var zone = Zone(2);

        Assert.Equal(1000, StatCalculator.RankBuffElementDefensePowerBonus(zone));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
    }

    [Fact]
    public void Tier3_BoostsElementAttackPower_By1000_AndNothingElse()
    {
        var zone = Zone(3);

        Assert.Equal(1000, StatCalculator.RankBuffElementAttackPowerBonus(zone));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
    }

    [Fact]
    public void Tier4_BoostsAttackBlock_By1000_AndNothingElse()
    {
        var zone = Zone(4);

        Assert.Equal(1000, StatCalculator.RankBuffAttackBlockBonus(zone));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
    }

    [Fact]
    public void Tier5_BoostsAttackSuccess_By1000_AndNothingElse()
    {
        var zone = Zone(5);

        Assert.Equal(1000, StatCalculator.RankBuffAttackSuccessBonus(zone));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
    }

    [Fact]
    public void Tier6_BoostsMaxLife_By1000_AndNothingElse()
    {
        var zone = Zone(6);

        Assert.Equal(1000, StatCalculator.RankBuffMaxLifeBonus(zone));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
    }

    [Fact]
    public void Tier7_BoostsAttackPower_By500_TheSoleException_AndNothingElse()
    {
        var zone = Zone(7);

        Assert.Equal(500, StatCalculator.RankBuffAttackPowerBonus(zone));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(100)]
    public void NoActiveOrOutOfRangeTier_ContributesZeroToEveryStat(int rankBuffType)
    {
        var zone = Zone(rankBuffType);

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
    }


    [Theory]
    [InlineData((short)124)]
    [InlineData((short)335)]
    public void SuppressedZone_ContributesZeroForEveryTier(short suppressedZone)
    {
        for (var tier = 1; tier <= 7; tier++)
        {
            var zone = Zone(tier, suppressedZone);

            Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
            Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
            Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
            Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
            Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
            Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
            Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
        }
    }

    [Fact]
    public void SuppressedZone335_AlsoSuppressesTheHpTier_WhichFfaOverrideDoesNotTouch()
    {
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(Zone(6, 335)));
        Assert.Equal(1000, StatCalculator.RankBuffMaxLifeBonus(Zone(6, NeutralZone)));
    }

    [Fact]
    public void SuppressedZones_AreExactly124And335_AdjacentZonesStillApply()
    {
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 123)));
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 125)));
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 334)));
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 336)));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 124)));
        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 335)));
    }


    [Fact]
    public void EveryTierExcept7_ContributesExactly1000_ToItsOwnStat()
    {
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1)));
        Assert.Equal(1000, StatCalculator.RankBuffElementDefensePowerBonus(Zone(2)));
        Assert.Equal(1000, StatCalculator.RankBuffElementAttackPowerBonus(Zone(3)));
        Assert.Equal(1000, StatCalculator.RankBuffAttackBlockBonus(Zone(4)));
        Assert.Equal(1000, StatCalculator.RankBuffAttackSuccessBonus(Zone(5)));
        Assert.Equal(1000, StatCalculator.RankBuffMaxLifeBonus(Zone(6)));

        Assert.Equal(500, StatCalculator.RankBuffAttackPowerBonus(Zone(7)));
    }


    [Fact]
    public void DefaultZoneContext_ContributesZero()
    {
        var zone = default(ZoneContext);

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackPowerBonus(zone));
    }
}
