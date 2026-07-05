namespace Fenrir.Application.Game.Skills;

/// <summary>One occupied learned-skill slot -- legacy <c>aSkill[slot][0..1]</c> (SkillId, Grade/<c>sPoint</c>).</summary>
public readonly record struct LearnedSkill(int SkillId, int Grade);
