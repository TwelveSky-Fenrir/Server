using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.Skills;

public class FormationSkillCatalogTests
{
    private const short OrdinaryZoneId = 1;

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
    [InlineData(78)]
    [InlineData(80)]
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
    [InlineData(78, 64)]
    [InlineData(80, 65)]
    [InlineData(5, 64)]
    [InlineData(1, 65)]
    public void NextPartyBuffMarker_NonPartyBuffSkill_LeavesMarkerUnchanged(int skillNumber, int actionSort)
    {
        var next = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.Cast, skillNumber, actionSort);

        Assert.Equal(PartyBuffAction.Cast, next);
    }

    [Fact]
    public void NextPartyBuffMarker_SinglePacketAdvancesAtMostOneStep()
    {
        var afterArm = FormationSkillCatalog.NextPartyBuffMarker(PartyBuffAction.None, 76,
            FormationSkillCatalog.PartyBuffArmActionSort);
        Assert.Equal(PartyBuffAction.Cast, afterArm);

        var afterConfirm = FormationSkillCatalog.NextPartyBuffMarker(afterArm, 76,
            FormationSkillCatalog.PartyBuffConfirmActionSort);
        Assert.Equal(PartyBuffAction.Done, afterConfirm);

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
    [InlineData(5, 16)]
    [InlineData(100, 0)]
    public void IsExemptFromGradeBoundCheck_False_ForDefaultCaseSkills(int skillNumber, int actionSort)
    {
        Assert.False(FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort,
            isPrimaryHandler: true));
        Assert.False(FormationSkillCatalog.IsExemptFromGradeBoundCheck(skillNumber, actionSort,
            isPrimaryHandler: false));
    }
}
