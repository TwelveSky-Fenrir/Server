namespace Fenrir.Application.Game.Domain.World;

public static class PartyBuffMarkerDispatchRules
{
    public static bool ShouldAdvancePartyBuffMarker(bool isResumeAction, int actionSort)
    {
        if (!isResumeAction)
            return actionSort is FormationSkillCatalog.PartyBuffArmActionSort
                or FormationSkillCatalog.PartyBuffConfirmActionSort;

        return actionSort == FormationSkillCatalog.PartyBuffArmActionSort;
    }

    public static bool ShouldResetPartyBuffMarkerOnConfirmSuccess(int skillNumber)
    {
        return FormationSkillCatalog.IsPartyBuffSkill(skillNumber);
    }
}
