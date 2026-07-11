using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B7 (tribe master/sub-master role combat-stat bonus): the flat, late tribe-role term folded
///     into three runtime getters -- DMG (<c>GetAttackPower</c>, <c>MyFactor.cpp:4055-4062</c>), DEF
///     (<c>GetDefensePower</c>, <c>MyFactor.cpp:4131-4138</c>), and CRIT (<c>GetCritical</c>,
///     <c>MyFactor.cpp:4450-4452</c>). Master (role 1) adds +200 DMG / +200 DEF / +2 CRIT; a sub-master (role 2)
///     adds +100 DMG / +100 DEF but nothing on CRIT; everyone else adds nothing; zone 124 suppresses the whole
///     term.
///     <para>
///         These tests drive the pure contribution accessors directly. The three getter call sites are wired
///         into the EFFECTIVE <see cref="StatCalculator.ComputeEffectiveStats" /> layer in a separate serial
///         integration pass, so the additive terms cannot yet be observed end-to-end through
///         <see cref="StatCalculator.ComputeEffectiveStats" /> itself.
///     </para>
/// </summary>
public class TribeRoleContributionTests
{
    private const short NeutralZone = 1; // an ordinary zone with no tribe-role suppression
    private const byte RoleRegular = 0;
    private const byte RoleMaster = 1;
    private const byte RoleSubMaster = 2;
    private const byte RoleCouncil = 3; // elected vote candidate -- no combat bonus

    private static ZoneContext Zone(byte tribeRole, short zoneNumber = NeutralZone)
    {
        return new ZoneContext(zoneNumber, TribeRole: tribeRole);
    }

    // ---- Master (role 1): +200 DMG, +200 DEF, +2 CRIT ----

    [Fact]
    public void Master_Adds200ToAttackAndDefense_And2ToCritical()
    {
        var zone = Zone(RoleMaster);

        Assert.Equal(200, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(200, StatCalculator.TribeRoleDefensePowerBonus(zone));
        Assert.Equal(2, StatCalculator.TribeRoleCriticalBonus(zone));
    }

    // ---- Sub-master (role 2): +100 DMG, +100 DEF, and NOTHING on CRIT (the asymmetry) ----

    [Fact]
    public void SubMaster_Adds100ToAttackAndDefense_ButNothingToCritical()
    {
        var zone = Zone(RoleSubMaster);

        Assert.Equal(100, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(100, StatCalculator.TribeRoleDefensePowerBonus(zone));

        // The master/sub-master asymmetry: a sub-master earns no critical bonus.
        Assert.Equal(0, StatCalculator.TribeRoleCriticalBonus(zone));
    }

    // ---- Regular / council / out-of-range roles: nothing on any stat ----

    [Theory]
    [InlineData(RoleRegular)] // regular member
    [InlineData(RoleCouncil)] // elected vote candidate -- never earns a combat bonus
    [InlineData((byte)4)] // above the {0,1,2,3} decode domain
    [InlineData((byte)200)] // any other value
    public void RegularCouncilOrUnknownRole_ContributesZeroToEveryStat(byte tribeRole)
    {
        var zone = Zone(tribeRole);

        Assert.Equal(0, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleCriticalBonus(zone));
    }

    // ---- Attack and defense are identical for every role ----

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

    // ---- Critical only ever rewards the master, and only by +2 ----

    [Theory]
    [InlineData(RoleRegular, 0)]
    [InlineData(RoleMaster, 2)]
    [InlineData(RoleSubMaster, 0)]
    [InlineData(RoleCouncil, 0)]
    public void CriticalRewardsOnlyTheMaster(byte tribeRole, int expected)
    {
        Assert.Equal(expected, StatCalculator.TribeRoleCriticalBonus(Zone(tribeRole)));
    }

    // ---- Zone 124 suppresses the whole term for every role and every stat ----

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
        // Guard against an off-by-one in the suppression set: neighbours of 124 still apply the full bonus.
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
        // The tribe-role contract cites ONLY the 124 gate. Zone 335 (FFA) is handled downstream by
        // FfaEqualStatsOverride clobbering the effective combat stats, so it is deliberately not modelled here.
        Assert.Equal(200, StatCalculator.TribeRoleAttackPowerBonus(Zone(RoleMaster, 335)));
        Assert.Equal(200, StatCalculator.TribeRoleDefensePowerBonus(Zone(RoleMaster, 335)));
        Assert.Equal(2, StatCalculator.TribeRoleCriticalBonus(Zone(RoleMaster, 335)));
    }

    // ---- The bonus is flat: it does not vary with any other ZoneContext field ----

    [Fact]
    public void BonusIsFlat_UnaffectedByOtherZoneContextState()
    {
        // Populate unrelated zone-context fields (rank-buff tier, ornament, guild buff); the tribe-role term
        // must be exactly the same flat value regardless.
        var loaded = new ZoneContext(
            NeutralZone,
            OrnamentInUse: true,
            RankBuffType: 7,
            TribeRole: RoleMaster,
            GuildBuffActive: true,
            GuildId: 42);

        Assert.Equal(200, StatCalculator.TribeRoleAttackPowerBonus(loaded));
        Assert.Equal(200, StatCalculator.TribeRoleDefensePowerBonus(loaded));
        Assert.Equal(2, StatCalculator.TribeRoleCriticalBonus(loaded));
    }

    // ---- A default (all-zero) ZoneContext contributes nothing anywhere ----

    [Fact]
    public void DefaultZoneContext_ContributesZero()
    {
        var zone = default(ZoneContext);

        Assert.Equal(0, StatCalculator.TribeRoleAttackPowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleDefensePowerBonus(zone));
        Assert.Equal(0, StatCalculator.TribeRoleCriticalBonus(zone));
    }
}
