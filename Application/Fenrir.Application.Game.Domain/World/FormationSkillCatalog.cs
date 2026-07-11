using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World;

public enum PartyBuffAction : byte
{
    None = 0,

    Cast = 1,

    Done = 2
}

public static class FormationSkillCatalog
{
    public const short Zone124TypeMapId = 124;

    public const int BottleSkillNumber = 1;

    public const int BottleExemptionActionSort = 16;

    public const int PartyBuffArmActionSort = 64;

    public const int PartyBuffConfirmActionSort = 65;

    private static readonly FrozenSet<int> FormationSkillNumbers = new[] { 76, 77, 78, 79, 80, 81 }.ToFrozenSet();

    private static readonly FrozenSet<int> PartyBuffSkillNumbers = new[] { 76, 77, 79, 81 }.ToFrozenSet();

    private static readonly FrozenSet<int> EmptyCaseSkillNumbers = new[] { 78, 80 }.ToFrozenSet();

    public static bool IsFormationSkill(int skillNumber)
    {
        return FormationSkillNumbers.Contains(skillNumber);
    }

    public static bool IsPartyBuffSkill(int skillNumber)
    {
        return PartyBuffSkillNumbers.Contains(skillNumber);
    }

    public static bool IsEmptyCaseSkill(int skillNumber)
    {
        return EmptyCaseSkillNumbers.Contains(skillNumber);
    }

    public static bool IsFormationSkillZoneLocked(int skillNumber, short mapId, bool isRegularWarMap)
    {
        if (!IsFormationSkill(skillNumber))
            return false;

        return mapId == Zone124TypeMapId || isRegularWarMap;
    }

    public static PartyBuffAction NextPartyBuffMarker(PartyBuffAction current, int skillNumber, int actionSort)
    {
        if (!IsPartyBuffSkill(skillNumber))
            return current;

        return actionSort switch
        {
            PartyBuffArmActionSort => PartyBuffAction.Cast,
            PartyBuffConfirmActionSort when current == PartyBuffAction.Cast => PartyBuffAction.Done,
            _ => current
        };
    }

    public static bool IsExemptFromGradeBoundCheck(int skillNumber, int actionSort, bool isPrimaryHandler)
    {
        if (IsPartyBuffSkill(skillNumber) || IsEmptyCaseSkill(skillNumber))
            return true;

        return isPrimaryHandler && skillNumber == BottleSkillNumber && actionSort == BottleExemptionActionSort;
    }
}
