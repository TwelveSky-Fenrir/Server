using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.Skills;

public class PartyBuffMarkerDispatchRulesTests
{

    [Fact]
    public void ShouldAdvance_Op15_Cast_True()
    {
        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: false,
            FormationSkillCatalog.PartyBuffArmActionSort));
    }

    [Fact]
    public void ShouldAdvance_Op15_Done_True()
    {
        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: false,
            FormationSkillCatalog.PartyBuffConfirmActionSort));
    }

    [Fact]
    public void ShouldAdvance_Op16_Cast_True()
    {
        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: true,
            FormationSkillCatalog.PartyBuffArmActionSort));
    }

    [Fact]
    public void ShouldAdvance_Op16_Done_False()
    {
        Assert.False(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: true,
            FormationSkillCatalog.PartyBuffConfirmActionSort));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 0)]
    public void ShouldAdvance_UnrelatedSort_False_OnEitherOpcode(bool isResumeAction, int actionSort)
    {
        Assert.False(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction, actionSort));
    }


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
    [InlineData(78)]
    [InlineData(80)]
    [InlineData(1)]
    [InlineData(82)]
    public void ShouldReset_False_ForNonPartyBuffSkills(int skillNumber)
    {
        Assert.False(PartyBuffMarkerDispatchRules.ShouldResetPartyBuffMarkerOnConfirmSuccess(skillNumber));
    }


    [Fact]
    public void FullHandshake_Cast64OnOp15_Done65OnOp15_ThenResetOnConfirmSuccess()
    {
        var marker = PartyBuffAction.None;

        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: false,
            FormationSkillCatalog.PartyBuffArmActionSort));
        marker = FormationSkillCatalog.NextPartyBuffMarker(marker, 76, FormationSkillCatalog.PartyBuffArmActionSort);
        Assert.Equal(PartyBuffAction.Cast, marker);

        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: false,
            FormationSkillCatalog.PartyBuffConfirmActionSort));
        marker = FormationSkillCatalog.NextPartyBuffMarker(marker, 76,
            FormationSkillCatalog.PartyBuffConfirmActionSort);
        Assert.Equal(PartyBuffAction.Done, marker);

        Assert.True(PartyBuffMarkerDispatchRules.ShouldResetPartyBuffMarkerOnConfirmSuccess(76));
        marker = PartyBuffAction.None;
        Assert.Equal(PartyBuffAction.None, marker);
    }

    [Fact]
    public void FullHandshake_Cast64OnOp16_ThenDone65OnOp16_NeverAdvances()
    {
        var marker = PartyBuffAction.None;

        Assert.True(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: true,
            FormationSkillCatalog.PartyBuffArmActionSort));
        marker = FormationSkillCatalog.NextPartyBuffMarker(marker, 77, FormationSkillCatalog.PartyBuffArmActionSort);
        Assert.Equal(PartyBuffAction.Cast, marker);

        Assert.False(PartyBuffMarkerDispatchRules.ShouldAdvancePartyBuffMarker(isResumeAction: true,
            FormationSkillCatalog.PartyBuffConfirmActionSort));
        Assert.Equal(PartyBuffAction.Cast, marker);
    }
}
