using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.Skills;

/// <summary>
///     Covers <see cref="FormationSkillCatalog" /> -- the B14 Formation-skill zone-lock, party-buff cast-lifecycle
///     marker transitions, and default-case grade-bound-check exemption classification (S04_MyWork02.cpp:1298-1310,
///     :1438-1450, :1684-1710; STRUCT.h:1417-1421).
/// </summary>
public class FormationSkillCatalogTests
{
    // A zone number that is neither the 124-type zone nor a Regular-War (049-type) map -- stands in for any
    // ordinary, non-war zone where Formation skills are never dropped.
    private const short OrdinaryZoneId = 1;

    // One of the fixed 11 Regular-War maps (RegularWarMapCatalog) -- but the zone-lock predicate takes the
    // "is a regular-war map" decision as an explicit bool, so the tests here pass it directly rather than
    // depending on that catalog.
    private const short RegularWarMapId = 49;

    [Theory]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(78)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void IsFormationSkill_True_ForTheContiguous76To81Set(int skillNumber)
    {
        Assert.True(FormationSkillCatalog.IsFormationSkill(skillNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(75)]
    [InlineData(82)]
    public void IsFormationSkill_False_OutsideThe76To81Set(int skillNumber)
    {
        Assert.False(FormationSkillCatalog.IsFormationSkill(skillNumber));
    }

    [Theory]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(79)]
    [InlineData(81)]
    public void IsPartyBuffSkill_True_ForTheFourMarkerDrivingSkills(int skillNumber)
    {
        Assert.True(FormationSkillCatalog.IsPartyBuffSkill(skillNumber));
    }

    [Theory]
    [InlineData(78)] // empty-case skill, not a party-buff skill
    [InlineData(80)] // empty-case skill, not a party-buff skill
    [InlineData(75)]
    [InlineData(82)]
    public void IsPartyBuffSkill_False_ForEmptyCaseAndNonFormationSkills(int skillNumber)
    {
        Assert.False(FormationSkillCatalog.IsPartyBuffSkill(skillNumber));
    }

    [Theory]
    [InlineData(78)]
    [InlineData(80)]
    public void IsEmptyCaseSkill_True_ForTheTwoNonBuffFormationSkills(int skillNumber)
    {
        Assert.True(FormationSkillCatalog.IsEmptyCaseSkill(skillNumber));
    }

    [Theory]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(79)]
    [InlineData(81)]
    [InlineData(1)]
    public void IsEmptyCaseSkill_False_ForPartyBuffAndNonFormationSkills(int skillNumber)
    {
        Assert.False(FormationSkillCatalog.IsEmptyCaseSkill(skillNumber));
    }

    [Theory]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(78)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void IsFormationSkillZoneLocked_True_InThe124TypeZone(int skillNumber)
    {
        // 124-type drop covers the full 76-81 set, regardless of the regular-war flag.
        Assert.True(FormationSkillCatalog.IsFormationSkillZoneLocked(skillNumber,
            FormationSkillCatalog.Zone124TypeMapId, isRegularWarMap: false));
    }

    [Theory]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(78)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    public void IsFormationSkillZoneLocked_True_InA049TypeRegularWarZone(int skillNumber)
    {
        // 049-type drop also covers the full 76-81 set (a separate legacy guard, identical outward behavior).
        Assert.True(FormationSkillCatalog.IsFormationSkillZoneLocked(skillNumber, RegularWarMapId,
            isRegularWarMap: true));
    }

    [Theory]
    [InlineData(76)]
    [InlineData(81)]
    public void IsFormationSkillZoneLocked_False_InAnOrdinaryNonWarZone(int skillNumber)
    {
        Assert.False(FormationSkillCatalog.IsFormationSkillZoneLocked(skillNumber, OrdinaryZoneId,
            isRegularWarMap: false));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(75)]
    [InlineData(82)]
    public void IsFormationSkillZoneLocked_False_ForNonFormationSkills_EvenInA124TypeZone(int skillNumber)
    {
        Assert.False(FormationSkillCatalog.IsFormationSkillZoneLocked(skillNumber,
            FormationSkillCatalog.Zone124TypeMapId, isRegularWarMap: true));
    }

    [Fact]
    public void NextPartyBuffMarker_Arm_SetsCast_FromNone()
    {
        var next = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.None, 76,
            FormationSkillCatalog.PartyBuffArmActionSort);

        Assert.Equal(PartyBuffAction.Cast, next);
    }

    [Fact]
    public void NextPartyBuffMarker_Arm_ResetsToCast_EvenFromDone()
    {
        // Sort 64 sets CAST unconditionally -- a new handshake can begin even while a prior one reached DONE.
        var next = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.Done, 77,
            FormationSkillCatalog.PartyBuffArmActionSort);

        Assert.Equal(PartyBuffAction.Cast, next);
    }

    [Fact]
    public void NextPartyBuffMarker_Confirm_AdvancesToDone_OnlyFromCast()
    {
        var next = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.Cast, 79,
            FormationSkillCatalog.PartyBuffConfirmActionSort);

        Assert.Equal(PartyBuffAction.Done, next);
    }

    [Fact]
    public void NextPartyBuffMarker_Confirm_FromNone_StaysNone_NoDirectNoneToDonePath()
    {
        var next = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.None, 81,
            FormationSkillCatalog.PartyBuffConfirmActionSort);

        Assert.Equal(PartyBuffAction.None, next);
    }

    [Fact]
    public void NextPartyBuffMarker_Confirm_FromDone_StaysDone()
    {
        var next = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.Done, 76,
            FormationSkillCatalog.PartyBuffConfirmActionSort);

        Assert.Equal(PartyBuffAction.Done, next);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(30)]
    public void NextPartyBuffMarker_UnrelatedSort_LeavesMarkerUnchanged(int actionSort)
    {
        var next = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.Cast, 76, actionSort);

        Assert.Equal(PartyBuffAction.Cast, next);
    }

    [Theory]
    [InlineData(78, 64)] // empty-case Formation skill never advances the marker
    [InlineData(80, 65)] // empty-case Formation skill never advances the marker
    [InlineData(5, 64)]  // non-Formation skill never advances the marker
    [InlineData(1, 65)]  // Bottle never advances the marker
    public void NextPartyBuffMarker_NonPartyBuffSkill_LeavesMarkerUnchanged(int skillNumber, int actionSort)
    {
        var next = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.Cast, skillNumber, actionSort);

        Assert.Equal(PartyBuffAction.Cast, next);
    }

    [Fact]
    public void NextPartyBuffMarker_SinglePacketAdvancesAtMostOneStep()
    {
        // None -> (sort 64) -> Cast -> (sort 65) -> Done, but no single packet can jump None -> Done.
        var afterArm = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.None, 76,
            FormationSkillCatalog.PartyBuffArmActionSort);
        Assert.Equal(PartyBuffAction.Cast, afterArm);

        var afterConfirm = FormationSkillCatalog.NextPartyBuffMarker(afterArm, 76,
            FormationSkillCatalog.PartyBuffConfirmActionSort);
        Assert.Equal(PartyBuffAction.Done, afterConfirm);

        // A confirm arriving first (marker still None) never reaches Done.
        var confirmFirst = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.None, 76,
            FormationSkillCatalog.PartyBuffConfirmActionSort);
        Assert.Equal(PartyBuffAction.None, confirmFirst);
    }

    [Theory]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(79)]
    [InlineData(81)]
    public void IsExemptFromGradeBoundCheck_True_ForPartyBuffSkills_InBothHandlers(int skillNumber)
    {
        Assert.True(FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort: 64,
            isPrimaryHandler: true));
        Assert.True(FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort: 65,
            isPrimaryHandler: false));
    }

    [Theory]
    [InlineData(78)]
    [InlineData(80)]
    public void IsExemptFromGradeBoundCheck_True_ForEmptyCaseSkills_InBothHandlers(int skillNumber)
    {
        Assert.True(FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort: 0,
            isPrimaryHandler: true));
        Assert.True(FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort: 0,
            isPrimaryHandler: false));
    }

    [Fact]
    public void IsExemptFromGradeBoundCheck_BottleSort16_ExemptInPrimaryHandlerOnly()
    {
        Assert.True(FormationSkillCatalog.IsExemptFromGradeBoundCheck(FormationSkillCatalog.BottleSkillNumber,
            FormationSkillCatalog.BottleExemptionActionSort, isPrimaryHandler: true));

        // The update-action handler's mirror carries no Bottle exemption.
        Assert.False(FormationSkillCatalog.IsExemptFromGradeBoundCheck(FormationSkillCatalog.BottleSkillNumber,
            FormationSkillCatalog.BottleExemptionActionSort, isPrimaryHandler: false));
    }

    [Fact]
    public void IsExemptFromGradeBoundCheck_Bottle_NotExempt_ForOtherActionSort()
    {
        Assert.False(FormationSkillCatalog.IsExemptFromGradeBoundCheck(FormationSkillCatalog.BottleSkillNumber,
            actionSort: 15, isPrimaryHandler: true));
    }

    [Theory]
    [InlineData(5, 16)]  // an ordinary skill with sort 16 is not the Bottle exemption
    [InlineData(100, 0)]
    public void IsExemptFromGradeBoundCheck_False_ForDefaultCaseSkills(int skillNumber, int actionSort)
    {
        Assert.False(FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort,
            isPrimaryHandler: true));
        Assert.False(FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort,
            isPrimaryHandler: false));
    }
}
