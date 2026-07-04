using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Quests;

/// <summary>
///     Posted by <c>QuestProgressHandler</c> (opcode 36) AFTER the quest-state transition (and any money/
///     item reward) is already durably persisted (<c>usp_CharacterQuest_ApplyTransition</c>, D7 regime (b))
///     -- <see cref="Fenrir.Application.Game.World.Zone" />'s own tick just mirrors it into
///     <see cref="Fenrir.Application.Game.World.PlayerRuntimeState" />'s quest fields/<see cref="Inventory" />/
///     Experience/ContributionPoints, exactly the same additive-channel posture <see cref="Inventory.InventoryZoneCommand" />/
///     <see cref="Skills.SkillZoneCommand" /> already established (own channel/drain method, never edits
///     <c>Zone.DrainInbox</c>'s existing switch).
/// </summary>
/// <param name="ExperienceDelta">
///     A FLAT add to Experience (report 04 §5: <c>mUTIL.ProcessForExperience(user, qReward, 0, 0)</c> --
///     the reward value IS the gain, unlike a monster kill's own level-gap formula). Same "no level-up
///     recompute" posture as <see cref="Fenrir.Application.Game.World.Zone.GrantMonsterKillExperience" />
///     (a pre-existing Phase C/V3 gap, not introduced here -- see this feature's StructuredOutput open
///     issues).
/// </param>
/// <param name="ContributionPointsDelta">A flat add to ContributionPoints (aKillOtherTribe/CP, report 04 §5 reward type 3) -- write-behind, same regime as <see cref="ExperienceDelta" />.</param>
/// <param name="Containers">Populated only when this transition touched an inventory container (quest-item deposit/reward-item deposit/exchange swap); empty otherwise.</param>
/// <param name="Applied">
///     Completed by <c>Zone.ApplyQuestCommand</c> the instant the tick mirrors this -- the SAME
///     await-before-releasing-EconomyActionLock contract <see cref="Inventory.InventoryZoneCommand.Applied" />
///     documents, required so a back-to-back second quest/pickup/inventory action for this character never
///     reads a stale pre-mirror snapshot.
/// </param>
/// <param name="TeacherPointDelta">A flat add to TeacherPoint (quest reward type 5, report 04 §5, GL_614_QUEST_TEACHER_POINT) -- write-behind, same regime as <see cref="ContributionPointsDelta" />.</param>
public readonly record struct QuestZoneCommand(
    int CharacterId,
    QuestProgress Progress,
    int ExperienceDelta,
    int ContributionPointsDelta,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    TaskCompletionSource? Applied = null,
    int TeacherPointDelta = 0);
