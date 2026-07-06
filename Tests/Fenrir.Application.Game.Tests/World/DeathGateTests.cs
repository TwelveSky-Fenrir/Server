using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Pure-rule coverage for <see cref="ReviveEligibilityZones" />, <see cref="ReviveEligibilityRules" />, and
///     <see cref="ZoneTransferAntiAbuseRules" /> -- the territorial revive-eligibility gate and its
///     zone-transfer companion check, independent of any <see cref="Zone" /> wiring.
/// </summary>
public class DeathGateTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(6, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 1)]
    [InlineData(11, 2)]
    [InlineData(12, 2)]
    [InlineData(13, 2)]
    [InlineData(14, 2)]
    [InlineData(140, 3)]
    [InlineData(141, 3)]
    [InlineData(142, 3)]
    [InlineData(143, 3)]
    public void Classify_FactionTerritoryBlocks_ResolveTheOwningFaction(short mapId, byte expectedOwner)
    {
        var (kind, owner) = ReviveEligibilityZones.Classify(mapId);

        Assert.Equal(ReviveZoneKind.FactionTerritory, kind);
        Assert.Equal(expectedOwner, owner);
    }

    [Fact]
    public void Classify_Zone200_IsAlwaysBlocked()
    {
        var (kind, _) = ReviveEligibilityZones.Classify(200);

        Assert.Equal(ReviveZoneKind.AlwaysBlocked, kind);
    }

    [Theory]
    [InlineData(322)]
    [InlineData(323)]
    [InlineData(5)] // the gap between the faction-0 and faction-1 blocks
    [InlineData(10)] // the gap between the faction-1 and faction-2 blocks
    [InlineData(999)]
    public void Classify_EverythingElse_IsUnconditional(short mapId)
    {
        var (kind, _) = ReviveEligibilityZones.Classify(mapId);

        Assert.Equal(ReviveZoneKind.Unconditional, kind);
    }

    [Fact]
    public void IsEligible_FactionTerritory_OwningFactionMatch_IsEligible()
    {
        Assert.True(ReviveEligibilityRules.IsEligible(2, 0, null));
    }

    [Fact]
    public void IsEligible_FactionTerritory_AlliedFactionMatch_IsEligible()
    {
        // Avatar is tribe 1, dead on a faction-0 territory block, but tribe 1 is currently allied with tribe 0.
        Assert.True(ReviveEligibilityRules.IsEligible(2, 1, 0));
    }

    [Fact]
    public void IsEligible_FactionTerritory_NoMatchAndNoAlliance_IsNotEligible()
    {
        Assert.False(ReviveEligibilityRules.IsEligible(2, 1, null));
    }

    [Fact]
    public void IsEligible_FactionTerritory_AlliedWithADifferentFaction_IsNotEligible()
    {
        // Tribe 1 allied with tribe 2 does not help against a faction-0 owned block.
        Assert.False(ReviveEligibilityRules.IsEligible(2, 1, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void IsEligible_AlwaysBlockedZone_IsNeverEligible_RegardlessOfFactionOrAlliance(byte tribe)
    {
        Assert.False(ReviveEligibilityRules.IsEligible(200, tribe, tribe));
    }

    [Theory]
    [InlineData(322)]
    [InlineData(323)]
    [InlineData(999)]
    public void IsEligible_UnconditionalZone_IsAlwaysEligible_RegardlessOfFactionOrAlliance(short mapId)
    {
        Assert.True(ReviveEligibilityRules.IsEligible(mapId, 3, null));
    }

    [Fact]
    public void ZoneTransfer_DestinationZone38_IsAlwaysAllowed_RegardlessOfCurrentZoneOrFaction()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(
            2, 38, 1, _ => null);

        Assert.True(allowed);
    }

    [Fact]
    public void ZoneTransfer_CurrentZoneNotFactionTerritory_IsAlwaysAllowed()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(
            999, 50, 1, _ => null);

        Assert.True(allowed);
    }

    [Fact]
    public void ZoneTransfer_FactionTerritory_AvatarMatchesOwningFaction_IsAllowed()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(
            2, 50, 0, _ => null);

        Assert.True(allowed);
    }

    [Fact]
    public void ZoneTransfer_Faction0Block_NeverGrantsAllianceLeniency_EvenWhenOwningFactionHasSomeAlly()
    {
        // Legacy quirk: faction-0 territory's companion check never grants alliance-based leniency at all --
        // only an exact faction-0 match on the avatar avoids the kick. Here the owning faction (0) is
        // "allied" with faction 2, which must NOT suspend the kick.
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(
            2, 50, 1, owner => owner == 0 ? 2 : null);

        Assert.False(allowed);
    }

    [Fact]
    public void ZoneTransfer_NonFaction0Block_OwningFactionAlliedWithFaction0_SuspendsKick_ForEveryAvatar()
    {
        // Legacy quirk: for the OTHER three blocks, an owning-faction alliance with faction 0 SPECIFICALLY
        // suspends the kick for every avatar leaving the zone, not just members of the allied faction.
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(
            7, 50, 3, owner => owner == 1 ? 0 : null);

        Assert.True(allowed);
    }

    [Fact]
    public void ZoneTransfer_NonFaction0Block_OwningFactionAlliedWithNonZeroFaction_DoesNotSuspendKick()
    {
        // The quirk specifically keys on faction 0 -- an alliance with any other faction does not help.
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(
            7, 50, 3, owner => owner == 1 ? 2 : null);

        Assert.False(allowed);
    }
}
