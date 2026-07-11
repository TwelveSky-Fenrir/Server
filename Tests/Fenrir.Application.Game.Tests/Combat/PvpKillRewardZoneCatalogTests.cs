using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillRewardZoneCatalogTests
{
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
        var profile = PvpKillRewardZoneCatalog.Resolve(zoneId, true);

        Assert.False(profile.GrantContributionPoints);
        Assert.True(profile.GrantExperience);
        Assert.True(profile.GrantDrop);
        Assert.True(profile.GrantDailyMissionProgress);
        Assert.Equal(0, profile.HeroPointAmount);
    }
}
