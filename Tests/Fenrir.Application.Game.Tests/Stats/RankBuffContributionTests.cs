using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B7: the rank-buff per-tier stat contribution
///     (<c>Server/Header/Protocol/MyFactor.cpp</c> -- the seven <c>aRankBuffType</c> consumption sites at
///     :2131/4038/4119/4198/4247/4314/4365). Each active tier (1-7) boosts exactly one stat by a flat amount
///     (+1000 for tiers 1-6, +500 for tier 7), suppressed entirely in zones 124 and 335.
///     <para>
///         These tests drive the pure contribution accessors directly: the seven getter call sites are wired in
///         a separate serial integration pass (tier 6 into the BASE max-life cache, tiers 1-5,7 into the
///         EFFECTIVE <c>ComputeEffectiveStats</c> layer), so the additive terms cannot yet be observed through
///         <see cref="StatCalculator.ComputeBaseStats" />/<see cref="StatCalculator.ComputeEffectiveStats" />.
///     </para>
/// </summary>
public class RankBuffContributionTests
{
    private const short NeutralZone = 1; // an ordinary zone with no rank-buff suppression

    private static ZoneContext Zone(int rankBuffType, short zoneNumber = NeutralZone)
    {
        return new ZoneContext(zoneNumber, RankBuffType: rankBuffType);
    }

    // ---- Each tier boosts exactly its own stat, by the cited magnitude ----

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

        // The one tier whose magnitude is NOT 1000.
        Assert.Equal(500, StatCalculator.RankBuffAttackPowerBonus(zone));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffElementAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackBlockBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffAttackSuccessBonus(zone));
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(zone));
    }

    // ---- "No tier active" and out-of-range are always zero for every stat ----

    [Theory]
    [InlineData(0)] // none active
    [InlineData(-1)] // below range
    [InlineData(8)] // above range
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

    // ---- Zone suppression: zones 124 and 335 suppress every tier, for every stat ----

    [Theory]
    [InlineData((short)124)] // hard-coded stat-neutral zone
    [InlineData((short)335)] // FFAMAPNUM (FFA equal-stats)
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
        // Tier 6 (HP) matters specifically for zone 335: Fenrir's separate FfaEqualStatsOverride leaves MaxLife
        // untouched, so this contribution-level suppression is what keeps the HP rank-buff from leaking in 335.
        Assert.Equal(0, StatCalculator.RankBuffMaxLifeBonus(Zone(6, 335)));
        // ...but in an ordinary zone the same tier does contribute.
        Assert.Equal(1000, StatCalculator.RankBuffMaxLifeBonus(Zone(6, NeutralZone)));
    }

    [Fact]
    public void SuppressedZones_AreExactly124And335_AdjacentZonesStillApply()
    {
        // Guard against an off-by-one in the suppression set: neighbours of both suppressed ids still apply.
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 123)));
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 125)));
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 334)));
        Assert.Equal(1000, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 336)));

        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 124)));
        Assert.Equal(0, StatCalculator.RankBuffDefensePowerBonus(Zone(1, 335)));
    }

    // ---- All six +1000 tiers agree on magnitude; tier 7 is the lone +500 ----

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

    // ---- A default (all-zero) ZoneContext contributes nothing anywhere ----

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
