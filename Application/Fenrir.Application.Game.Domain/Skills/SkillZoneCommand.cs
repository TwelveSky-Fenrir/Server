namespace Fenrir.Application.Game.Domain.Skills;

public readonly record struct SkillZoneCommand(int CharacterId, byte Slot, LearnedSkill Skill, int NewSkillPoints);
