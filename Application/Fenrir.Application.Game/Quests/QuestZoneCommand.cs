using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Quests;

/// <summary>
///     Posted by <c>QuestProgressHandler</c> (opcode 36) AFTER the quest-state transition is already
///     durably persisted -- <see cref="Fenrir.Application.Game.World.Zone" />'s tick mirrors it into
///     <see cref="Fenrir.Application.Game.World.PlayerRuntimeState" />'s quest/inventory/experience/CP
///     fields. Same additive-channel posture as <see cref="Inventory.InventoryZoneCommand" />/
///     <see cref="Skills.SkillZoneCommand" /> (own channel, never touches <c>Zone.DrainInbox</c>'s switch).
/// </summary>
/// <param name="ExperienceDelta">
///     Flat add to Experience (<c>mUTIL.ProcessForExperience(user, qReward, 0, 0)</c> -- the reward value IS
///     the gain, unlike a monster kill's level-gap formula). Same "no level-up recompute" gap as
///     <see cref="Fenrir.Application.Game.World.Zone.GrantMonsterKillExperience" /> -- pre-existing, not
///     introduced here.
/// </param>
/// <param name="ContributionPointsDelta">
///     Flat add to ContributionPoints (reward type 3) -- write-behind, same regime as
///     <see cref="ExperienceDelta" />.
/// </param>
/// <param name="Containers">
///     Populated only when this transition touched an inventory container (quest-item
///     deposit/reward-item deposit/exchange swap); empty otherwise.
/// </param>
/// <param name="Applied">
///     Completed by <c>Zone.ApplyQuestCommand</c> the instant the tick mirrors this -- same
///     await-before-releasing-<see cref="PlayerRuntimeState.EconomyActionLock" /> contract as
///     <see cref="Inventory.InventoryZoneCommand.Applied" />, so a back-to-back action for this character
///     never reads a stale pre-mirror snapshot.
/// </param>
/// <param name="TeacherPointDelta">
///     Flat add to TeacherPoint (reward type 5, GL_614_QUEST_TEACHER_POINT) -- write-behind,
///     same regime as <see cref="ContributionPointsDelta" />.
/// </param>
public readonly record struct QuestZoneCommand(
    int CharacterId,
    QuestProgress Progress,
    int ExperienceDelta,
    int ContributionPointsDelta,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    TaskCompletionSource? Applied = null,
    int TeacherPointDelta = 0);
