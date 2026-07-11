using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.Skills;

/// <summary>
///     Covers <see cref="PartyBuffMarkerDispatchRules" /> -- the B14 wave9 reconciliation of which opcode/Sort
///     combinations should call <see cref="Zone.AdvanceCasterPartyBuffMarker" /> (CAST reachable via either
///     opcode 15/16, DONE reachable via opcode 15 only) and which skill numbers consume the DONE state on a
///     successful CONFIRM. Combined with <see cref="FormationSkillCatalogTests" /> (which already covers
///     <see cref="FormationSkillCatalog.NextPartyBuffMarker" />'s own state-machine logic, unchanged by this
///     reconciliation), these two suites together pin the full corrected CAST/DONE/CONFIRM handshake without
///     needing to exercise <c>Zone.PlayerLifecycle.cs</c> directly.
/// </summary>
public class PartyBuffMarkerDispatchRulesTests
{
    // --- ShouldAdvancePartyBuffMarker -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvance_Op15_Cast_True()
    {
        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: false,
            FormationSkillCatalog.PartyBuffArmActionSort));
    }

    [Fact]
    public void ShouldAdvance_Op15_Done_True()
    {
        // DONE (65) is reachable via op15 -- the only opcode that can ever deliver it.
        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: false,
            FormationSkillCatalog.PartyBuffConfirmActionSort));
    }

    [Fact]
    public void ShouldAdvance_Op16_Cast_True()
    {
        // CAST (64) is reachable via EITHER opcode.
        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: true,
            FormationSkillCatalog.PartyBuffArmActionSort));
    }

    [Fact]
    public void ShouldAdvance_Op16_Done_False()
    {
        // DONE (65) is NEVER reachable via op16 -- a real client packet with this Sort/opcode pair never
        // reaches this call in the first place (AvatarActionResumeWhitelist already disconnects it), but the
        // predicate itself must still never claim it should advance the marker.
        Assert.False(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: true,
            FormationSkillCatalog.PartyBuffConfirmActionSort));
    }

    [Theory]
    [InlineData(false, 1)] // op15, the CONFIRM Sort (a different value space entirely from CAST/DONE)
    [InlineData(false, 0)] // op15, rest/stand-up
    [InlineData(true, 1)] // op16, CONFIRM Sort -- routed to ApplySkillEffectConfirm, not this predicate
    [InlineData(true, 0)] // op16, unrelated Sort
    public void ShouldAdvance_UnrelatedSort_False_OnEitherOpcode(bool isResumeAction, int actionSort)
    {
        Assert.False(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction, actionSort));
    }

    // --- ShouldResetPartyBuffMarkerOnConfirmSuccess ---------------------------------------------------------

    [Theory]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(79)]
    [InlineData(81)]
    public void ShouldReset_True_ForTheFourPartyBuffSkills(int skillNumber)
    {
        Assert.True(PartyBuffMarkerDispatchRules.ShouldResetPartyBuffMarkerOnConfirmSuccess(skillNumber));
    }

    [Theory]
    [InlineData(78)] // empty-case Formation skill, never touches the marker
    [InlineData(80)] // empty-case Formation skill, never touches the marker
    [InlineData(1)] // Bottle
    [InlineData(82)] // Holy Shield -- a self-buff, but not a party-buff (no full-party gate)
    public void ShouldReset_False_ForNonPartyBuffSkills(int skillNumber)
    {
        Assert.False(PartyBuffMarkerDispatchRules.ShouldResetPartyBuffMarkerOnConfirmSuccess(skillNumber));
    }

    // --- Combined handshake, end to end via the two pure catalogs together ---------------------------------

    [Fact]
    public void FullHandshake_Cast64OnOp15_Done65OnOp15_ThenResetOnConfirmSuccess()
    {
        var marker = PartyBuffAction.None;

        // Op15 CAST(64): should advance.
        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: false,
            FormationSkillCatalog.PartyBuffArmActionSort));
        marker = FormationSkillCatalog.NextPartyBuffMarker(marker, 76, FormationSkillCatalog.PartyBuffArmActionSort);
        Assert.Equal(PartyBuffAction.Cast, marker);

        // Op15 DONE(65): should advance.
        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: false,
            FormationSkillCatalog.PartyBuffConfirmActionSort));
        marker = FormationSkillCatalog.NextPartyBuffMarker(marker, 76,
            FormationSkillCatalog.PartyBuffConfirmActionSort);
        Assert.Equal(PartyBuffAction.Done, marker);

        // Op16 CONFIRM(1) succeeds and writes the buff: the marker is consumed (reset to None), not advanced
        // via NextPartyBuffMarker (Sort 1 is a different value space -- ShouldResetPartyBuffMarkerOnConfirmSuccess
        // is the correct consumer here, not ShouldAdvancePartyBuffMarker).
        Assert.True(PartyBuffMarkerDispatchRules.ShouldResetPartyBuffMarkerOnConfirmSuccess(76));
        marker = PartyBuffAction.None;
        Assert.Equal(PartyBuffAction.None, marker);
    }

    [Fact]
    public void FullHandshake_Cast64OnOp16_ThenDone65OnOp16_NeverAdvances()
    {
        // A hostile/buggy op16 client that tries to arm via op16 (legal) then send DONE via op16 (illegal at
        // the opcode level, per AvatarActionResumeWhitelist) never reaches Done through this predicate alone --
        // ShouldAdvancePartyBuffMarker correctly refuses the second step even independent of the disconnect
        // that would already have torn the session down before this call.
        var marker = PartyBuffAction.None;

        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: true,
            FormationSkillCatalog.PartyBuffArmActionSort));
        marker = FormationSkillCatalog.NextPartyBuffMarker(marker, 77, FormationSkillCatalog.PartyBuffArmActionSort);
        Assert.Equal(PartyBuffAction.Cast, marker);

        Assert.False(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: true,
            FormationSkillCatalog.PartyBuffConfirmActionSort));
        // Marker stays Cast -- the caller never even invokes NextPartyBuffMarker for this (opcode, Sort) pair.
        Assert.Equal(PartyBuffAction.Cast, marker);
    }
}
