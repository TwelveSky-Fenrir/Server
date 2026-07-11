using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

public class TribeRoleContributionTests
{
    private const short NeutralZone = 1;
    private const byte RoleRegular = 0;
    private const byte RoleMaster = 1;
    private const byte RoleSubMaster = 2;
    private const byte RoleCouncil = 3;

    private static ZoneContext Zone(byte tribeRole, short zoneNumber = NeutralZone)
    {
        return new ZoneContext(zoneNumber, TribeRole: tribeRole);
    }


    [Fact]
    public void Master_Adds200ToAttackAndDefense_And2ToCritical()
    {
        var zone = Zone(RoleMaster);

        Assert.Equal(200, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(200, StatCalculator.TribeRoleDefensePowerBonus(zone));
        Assert.Equal(2, StatCalculator.TribeRoleCriticalBonus(zone));
    }


    [Fact]
    public void SubMaster_Adds100ToAttackAndDefense_ButNothingToCritical()
    {
        var zone = Zone(RoleSubMaster);

        Assert.Equal(100, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(100, StatCalculator.TribeRoleDefensePowerBonus(zone));

        Assert.Equal(0, StatCalculator.TribeRoleCriticalBonus(zone));
    }


    [Theory]
    [InlineData(RoleRegular)]
    [InlineData(RoleCouncil)]
    [InlineData((byte)4)]
    [InlineData((byte)200)]
    public void RegularCouncilOrUnknownRole_ContributesZeroToEveryStat(byte tribeRole)
    {
        var zone = Zone(tribeRole);

        Assert.Equal(0, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleCriticalBonus(zone));
    }


    [Theory]
    [InlineData(RoleRegular, 0)]
    [InlineData(RoleMaster, 200)]
    [InlineData(RoleSubMaster, 100)]
    [InlineData(RoleCouncil, 0)]
    public void AttackAndDefenseAgreeForEveryRole(byte tribeRole, int expected)
    {
        var zone = Zone(tribeRole);

        Assert.Equal(expected, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(expected, StatCalculator.TribeRoleDefensePowerBonus(zone));
    }


    [Theory]
    [InlineData(RoleRegular, 0)]
    [InlineData(RoleMaster, 2)]
    [InlineData(RoleSubMaster, 0)]
    [InlineData(RoleCouncil, 0)]
    public void CriticalRewardsOnlyTheMaster(byte tribeRole, int expected)
    {
        Assert.Equal(expected, StatCalculator.TribeRoleCriticalBonus(Zone(tribeRole)));
    }


    [Theory]
    [InlineData(RoleMaster)]
    [InlineData(RoleSubMaster)]
    public void Zone124_SuppressesEveryStatForEveryRole(byte tribeRole)
    {
        var zone = Zone(tribeRole, 124);

        Assert.Equal(0, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleCriticalBonus(zone));
    }

    [Fact]
    public void SuppressedZone_IsExactly124_AdjacentZonesStillApply()
    {
        Assert.Equal(200, StatCalculator.TribeRoleAttackPowerBonus(Zone(RoleMaster, 123)));
        Assert.Equal(200, StatCalculator.TribeRoleAttackPowerBonus(Zone(RoleMaster, 125)));
        Assert.Equal(2, StatCalculator.TribeRoleCriticalBonus(Zone(RoleMaster, 123)));
        Assert.Equal(2, StatCalculator.TribeRoleCriticalBonus(Zone(RoleMaster, 125)));

        Assert.Equal(0, StatCalculator.TribeRoleAttackPowerBonus(Zone(RoleMaster, 124)));
        Assert.Equal(0, StatCalculator.TribeRoleCriticalBonus(Zone(RoleMaster, 124)));
    }

    [Fact]
    public void Zone335_IsNotSuppressedHere_UnlikeTheRankBuffSibling()
    {
        Assert.Equal(200, StatCalculator.TribeRoleAttackPowerBonus(Zone(RoleMaster, 335)));
        Assert.Equal(200, StatCalculator.TribeRoleDefensePowerBonus(Zone(RoleMaster, 335)));
        Assert.Equal(2, StatCalculator.TribeRoleCriticalBonus(Zone(RoleMaster, 335)));
    }


    [Fact]
    public void BonusIsFlat_UnaffectedByOtherZoneContextState()
    {
        var loaded = new ZoneContext(
            NeutralZone,
            true,
            RankBuffType: 7,
            TribeRole: RoleMaster,
            GuildBuffActive: true,
            GuildId: 42);

        Assert.Equal(200, StatCalculator.TribeRoleAttackPowerBonus(loaded));
        Assert.Equal(200, StatCalculator.TribeRoleDefensePowerBonus(loaded));
        Assert.Equal(2, StatCalculator.TribeRoleCriticalBonus(loaded));
    }


    [Fact]
    public void DefaultZoneContext_ContributesZero()
    {
        var zone = default(ZoneContext);

        Assert.Equal(0, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleCriticalBonus(zone));
    }
}
