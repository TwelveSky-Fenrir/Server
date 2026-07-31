namespace Fenrir.Application.Game.Domain.Movement;

public readonly record struct CharacterMotionEvaluation(
    int SkillCategoryCode,
    bool AttackBudgetEnforced,
    int AttackFamilyTag,
    int AttackSubPacketCeiling);
