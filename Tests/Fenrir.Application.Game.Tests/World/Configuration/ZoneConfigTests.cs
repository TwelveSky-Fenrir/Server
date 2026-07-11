using Fenrir.Application.Game.Domain.World.Configuration;

namespace Fenrir.Application.Game.Tests.World.Configuration;

public class ZoneConfigTests
{
    [Fact]
    public void Unconfigured_MatchesLegacyPerSlotDefault()
    {
        // Server/Header/S18_MyZoneInfo.cpp:15-18 -- level band 0-0, owner -1, secondary 0.
        var config = ZoneConfig.Unconfigured;

        Assert.Equal(0, config.MinLevel);
        Assert.Equal(0, config.MaxLevel);
        Assert.Equal(ZoneConfig.NoOwnerTribe, config.OwnerTribe);
        Assert.Equal(-1, config.OwnerTribe);
        Assert.Equal(0, config.SecondaryClassification);
        Assert.Equal(0, config.MaxUser);
    }

    [Fact]
    public void Unconfigured_LeavesEveryTunableNeutral()
    {
        var config = ZoneConfig.Unconfigured;

        Assert.Equal(0, config.GeneralExperienceRatio);
        Assert.Equal(0, config.PetExperienceRatio);
        Assert.Equal(0, config.MountExperienceRatio);
        Assert.Equal(0, config.BonusExperienceRatio);
        Assert.Equal(0, config.TeacherPointRatio);
        Assert.Equal(0, config.NormalItemDropRatio);
        Assert.Equal(0, config.RareItemDropRatio);
        Assert.Equal(0, config.EliteItemDropRatio);
        Assert.Equal(0, config.MoneyDropRatio);
        Assert.Equal(0, config.KillOtherTribeAddValue);
        Assert.Equal(0, config.KillOtherTribeExperienceRatio);
        Assert.Equal(0, config.WarCountdownTime);
        Assert.Equal(0, config.WarExperienceRatio);
        Assert.Equal(0, config.WarPvpRatio);
        Assert.Equal(0, config.WarMoneyRatio);
    }

    [Fact]
    public void DefaultStruct_OwnerTribeIsZero_NotMinusOne_WhichIsWhyUnconfiguredExists()
    {
        // Documents the trap the static Unconfigured exists to avoid: default(ZoneConfig) reports owner 0 (a real
        // tribe), NOT the legacy "no owner" -1. Callers that need the neutral snapshot must use Unconfigured.
        var raw = default(ZoneConfig);

        Assert.Equal(0, raw.OwnerTribe);
        Assert.NotEqual(ZoneConfig.Unconfigured, raw);
    }

    [Fact]
    public void WithExpression_ProducesStructurallyEqualCopies()
    {
        var a = new ZoneConfig { MinLevel = 100, MaxLevel = 145, OwnerTribe = 2, MaxUser = 500 };
        var b = a with { };
        var c = a with { MaxUser = 501 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void InitProperties_RoundTripEveryField()
    {
        var config = new ZoneConfig
        {
            MinLevel = 113,
            MaxLevel = 145,
            OwnerTribe = 10,
            SecondaryClassification = 1,
            MaxUser = 750,
            GeneralExperienceRatio = 200,
            SpecialZone38ExperienceRatio = 150,
            SpecialZone175MoneyRatio = 175,
            RegularWarZone49Window = 49,
            RegularWarZone51Window = 51,
            RegularWarZone53Window = 53,
            WarPvpRatio = 300
        };

        Assert.Equal(113, config.MinLevel);
        Assert.Equal(145, config.MaxLevel);
        Assert.Equal(10, config.OwnerTribe);
        Assert.Equal(1, config.SecondaryClassification);
        Assert.Equal(750, config.MaxUser);
        Assert.Equal(200, config.GeneralExperienceRatio);
        Assert.Equal(150, config.SpecialZone38ExperienceRatio);
        Assert.Equal(175, config.SpecialZone175MoneyRatio);
        Assert.Equal(49, config.RegularWarZone49Window);
        Assert.Equal(51, config.RegularWarZone51Window);
        Assert.Equal(53, config.RegularWarZone53Window);
        Assert.Equal(300, config.WarPvpRatio);
    }
}
