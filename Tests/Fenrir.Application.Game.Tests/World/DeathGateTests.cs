using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

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
    public void Classify_ZonePair322And323_IsDeathClearOnly(short mapId)
    {
        var (kind, _) = ReviveEligibilityZones.Classify(mapId);

        Assert.Equal(ReviveZoneKind.DeathClearOnly, kind);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(999)]
    public void Classify_EverythingElse_IsUnconditional(short mapId)
    {
        var (kind, _) = ReviveEligibilityZones.Classify(mapId);

        Assert.Equal(ReviveZoneKind.Unconditional, kind);
    }

    [Fact]
    public void Resolve_FactionTerritory_OwningFactionMatch_ClearsWindowAndLock()
    {
        Assert.Equal(ReviveClearOutcome.ClearDeathWindowAndLock, ReviveEligibilityRules.Resolve(2, 0, null));
    }

    [Fact]
    public void Resolve_FactionTerritory_AlliedFactionMatch_ClearsWindowAndLock()
    {
        Assert.Equal(ReviveClearOutcome.ClearDeathWindowAndLock, ReviveEligibilityRules.Resolve(2, 1, 0));
    }

    [Fact]
    public void Resolve_FactionTerritory_NoMatchAndNoAlliance_NoChange()
    {
        Assert.Equal(ReviveClearOutcome.NoChange, ReviveEligibilityRules.Resolve(2, 1, null));
    }

    [Fact]
    public void Resolve_FactionTerritory_AlliedWithADifferentFaction_NoChange()
    {
        Assert.Equal(ReviveClearOutcome.NoChange, ReviveEligibilityRules.Resolve(2, 1, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Resolve_AlwaysBlockedZone_NeverChanges_RegardlessOfFactionOrAlliance(byte tribe)
    {
        Assert.Equal(ReviveClearOutcome.NoChange, ReviveEligibilityRules.Resolve(200, tribe, tribe));
    }

    [Theory]
    [InlineData(322)]
    [InlineData(323)]
    public void Resolve_DeathClearOnlyZone_ClearsWindowButLeavesLockArmed_RegardlessOfFactionOrAlliance(short mapId)
    {
        Assert.Equal(ReviveClearOutcome.ClearDeathWindowOnly, ReviveEligibilityRules.Resolve(mapId, 3, null));
    }

    [Fact]
    public void Resolve_UnconditionalZone_ClearsWindowAndLock_RegardlessOfFactionOrAlliance()
    {
        Assert.Equal(ReviveClearOutcome.ClearDeathWindowAndLock, ReviveEligibilityRules.Resolve(999, 3, null));
    }

    [Fact]
    public void ZoneTransfer_DestinationZone38_IsAlwaysAllowed_RegardlessOfCapitalGroupOrAlliance()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(38, _ => null);

        Assert.True(allowed);
    }

    [Fact]
    public void ZoneTransfer_DestinationNotACapitalGroup_IsAlwaysAllowed()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(50, _ => null);

        Assert.True(allowed);
    }

    [Fact]
    public void ZoneTransfer_Tribe0CapitalGroup_AllianceCheckCanNeverBeSatisfied_RejectedWithoutTheHub()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(2, _ => null);

        Assert.False(allowed);
    }

    [Fact]
    public void ZoneTransfer_Tribe0CapitalGroup_EvenAnOwnTribeRequester_IsRejectedWithoutTheHub()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(2, _ => null);

        Assert.False(allowed);
    }

    [Fact]
    public void ZoneTransfer_Tribe1CapitalGroup_OwningFactionAlliedWithTribe0_AllowsAnyRequester()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(7, owner => owner == 1 ? (byte)0 : null);

        Assert.True(allowed);
    }

    [Fact]
    public void ZoneTransfer_Tribe1CapitalGroup_OwningFactionAlliedWithNonZeroTribe_Rejected()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(7, owner => owner == 1 ? (byte)2 : null);

        Assert.False(allowed);
    }

    [Fact]
    public void ZoneTransfer_Tribe1CapitalGroup_NoAllianceAtAll_Rejected()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(7, _ => null);

        Assert.False(allowed);
    }

    [Fact]
    public void ZoneTransfer_Tribe2CapitalGroup_OwningFactionAlliedWithTribe0_AllowsAnyRequester()
    {
        var allowed = ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(12, owner => owner == 2 ? (byte)0 : null);

        Assert.True(allowed);
    }

    [Fact]
    public void ZoneTransfer_Tribe3CapitalGroup_OwningFactionAlliedWithTribe0_AllowsAnyRequester()
    {
        var allowed =
            ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(141, owner => owner == 3 ? (byte)0 : null);

        Assert.True(allowed);
    }
}
