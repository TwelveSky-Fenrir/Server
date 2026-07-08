using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillRewardZoneCatalogTests
{
    // Zone id 999 stands in for any zone number not listed in any of the catalog's modeled groups (city,
    // FFA, unconditional-full) -- it deliberately does NOT collide with a real switch case label from
    // S07_MyGame03.cpp:2839-3043, unlike the zone id 1 previously (incorrectly) used here for these two
    // tests: zone 1 is actually one of the four "city" zones (see CityZone_* tests below), which do NOT
    // grant nothing on a stun kill -- only CP is withheld there.
    private const short UnlistedZoneId = 999;

    [Fact]
    public void FfaZone_DisablesGenericCpAndExpAndDailyMission_ButEnablesDropAndHeroPoint()
    {
        var profile = PvpKillRewardZoneCatalog.Resolve(PvpKillRewardZoneCatalog.FfaMapNumber, false);

        Assert.False(profile.GrantContributionPoints);
        Assert.False(profile.GrantExperience);
        Assert.True(profile.GrantDrop);
        Assert.False(profile.GrantDailyMissionProgress);
        Assert.Equal(PvpKillRewardZoneCatalog.FfaHeroPointAmount, profile.HeroPointAmount);
    }

    [Fact]
    public void FfaZone_IsUnaffectedByStunTrigger()
    {
        var normal = PvpKillRewardZoneCatalog.Resolve(PvpKillRewardZoneCatalog.FfaMapNumber, false);
        var stun = PvpKillRewardZoneCatalog.Resolve(PvpKillRewardZoneCatalog.FfaMapNumber, true);

        Assert.Equal(normal, stun);
    }

    [Theory]
    [InlineData((short)194)]
    [InlineData((short)267)]
    [InlineData((short)268)]
    [InlineData((short)269)]
    public void UnconditionalFullRewardZones_GrantEverythingEvenOnStunKill(short zoneId)
    {
        var profile = PvpKillRewardZoneCatalog.Resolve(zoneId, true);

        Assert.True(profile.GrantContributionPoints);
        Assert.True(profile.GrantExperience);
        Assert.True(profile.GrantDrop);
        Assert.True(profile.GrantDailyMissionProgress);
        Assert.Equal(0, profile.HeroPointAmount);
    }

    [Fact]
    public void DefaultZone_NonStunKill_GrantsEverythingExceptHeroPoint()
    {
        var profile = PvpKillRewardZoneCatalog.Resolve(UnlistedZoneId, false);

        Assert.True(profile.GrantContributionPoints);
        Assert.True(profile.GrantExperience);
        Assert.True(profile.GrantDrop);
        Assert.True(profile.GrantDailyMissionProgress);
        Assert.Equal(0, profile.HeroPointAmount);
    }

    [Fact]
    public void DefaultZone_StunKill_GrantsNothing()
    {
        var profile = PvpKillRewardZoneCatalog.Resolve(UnlistedZoneId, true);

        Assert.Equal(PvpKillZoneRewardProfile.None, profile);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)6)]
    [InlineData((short)11)]
    [InlineData((short)140)]
    public void CityZone_NonStunKill_GrantsEverythingExceptHeroPoint(short zoneId)
    {
        var profile = PvpKillRewardZoneCatalog.Resolve(zoneId, false);

        Assert.True(profile.GrantContributionPoints);
        Assert.True(profile.GrantExperience);
        Assert.True(profile.GrantDrop);
        Assert.True(profile.GrantDailyMissionProgress);
        Assert.Equal(0, profile.HeroPointAmount);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)6)]
    [InlineData((short)11)]
    [InlineData((short)140)]
    public void CityZone_StunKill_WithholdsOnlyContributionPoints(short zoneId)
    {
        // S07_MyGame03.cpp:2841-2852 -- unlike the default branch, a city-zone stun kill still grants
        // drop/EXP/daily-mission progress; only CP is gated on kill-type != stun.
        var profile = PvpKillRewardZoneCatalog.Resolve(zoneId, true);

        Assert.False(profile.GrantContributionPoints);
        Assert.True(profile.GrantExperience);
        Assert.True(profile.GrantDrop);
        Assert.True(profile.GrantDailyMissionProgress);
        Assert.Equal(0, profile.HeroPointAmount);
    }
}
