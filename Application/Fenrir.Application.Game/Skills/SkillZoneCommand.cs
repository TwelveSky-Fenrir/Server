namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Posted by an async skill handler after the learn/upgrade is already validated and durably persisted;
///     Zone's tick just mirrors it into PlayerRuntimeState -- no validation, no I/O, no business logic here.
/// </summary>
/// <param name="CharacterId">A no-op if the player already left the zone.</param>
/// <param name="Slot">The learned/upgraded skill's slot (0-39).</param>
/// <param name="Skill">The slot's new (SkillId, Grade) content.</param>
/// <param name="NewSkillPoints">
///     The resulting SkillPoints balance. Stays on the existing eventually-consistent write-behind path rather
///     than a second synchronous SQL write, to avoid a FlushSequence race against the periodic flusher.
/// </param>
public readonly record struct SkillZoneCommand(int CharacterId, byte Slot, LearnedSkill Skill, int NewSkillPoints);
