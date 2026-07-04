namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Posted by an async skill handler (<c>GenericActionHandler</c>, tSort 202/233/203) AFTER it has already
///     validated the learn/upgrade (<see cref="SkillLearnResolver" />) and durably persisted the slot via
///     <c>CharacterRepository.UpsertSkillSlotAsync</c> (D7 regime (b) -- the CharacterSkills twin of
///     <see cref="Inventory.InventoryZoneCommand" />). This command carries ONLY the already-decided,
///     already-durable result: <see cref="World.Zone" />'s own tick (the single mutator of
///     <see cref="World.PlayerRuntimeState" />) just mirrors it into
///     <see cref="World.PlayerRuntimeState.LearnedSkills" />/<see cref="World.PlayerRuntimeState.SkillPoints" />
///     -- no validation, no I/O, no business logic on the receiving end.
/// </summary>
/// <param name="CharacterId">Which player this command applies to -- a no-op if they already left the zone.</param>
/// <param name="Slot">The learned/upgraded skill's slot (0-39).</param>
/// <param name="Skill">The slot's new (SkillId, Grade) content.</param>
/// <param name="NewSkillPoints">
///     The resulting SkillPoints balance (already decremented by the handler from the in-memory value it
///     read) -- SkillPoints itself deliberately stays on the EXISTING eventually-consistent write-behind
///     path (D7 regime (a), same as Level/StatPoints/Experience) rather than a second synchronous SQL write:
///     see <c>usp_CharacterSkills_UpsertSlot</c>'s own header comment for why inventing a second synchronous
///     path for this ONE counter would risk a FlushSequence race against the periodic write-behind flusher.
/// </param>
public readonly record struct SkillZoneCommand(int CharacterId, byte Slot, LearnedSkill Skill, int NewSkillPoints);
