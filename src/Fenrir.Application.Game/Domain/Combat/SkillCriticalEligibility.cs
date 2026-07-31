using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Combat;

public static class SkillCriticalEligibility
{
    public const int ExcludedSkillNumber = 78;

    private const byte QualifyingAttackTypeA = 2;

    private const byte QualifyingAttackTypeB = 5;

    public static bool IsEligibleForSkillHit(int skillNumber, SkillDefinition? attackSkill)
    {
        return skillNumber != ExcludedSkillNumber &&
               attackSkill is { Skill.AttackType: QualifyingAttackTypeA or QualifyingAttackTypeB };
    }
}
