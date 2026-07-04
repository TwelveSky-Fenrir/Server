using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Progression;

/// <summary>
///     Posted by <c>DailyMissionHandler</c> (opcode 126, tSort=2 claim) AFTER the 4 mission counters
///     (AFTER deduction) and any reward-item deposit are already durably persisted
///     (<c>usp_Character_ApplyDailyMissionClaim</c>, D7 regime (b)) -- <c>Zone</c>'s own tick just mirrors
///     them onto the live <c>PlayerRuntimeState</c>/<c>Inventory</c>, the SAME additive-channel posture
///     <see cref="Fenrir.Application.Game.Quests.QuestZoneCommand" />/
///     <see cref="Fenrir.Application.Game.Skills.SkillZoneCommand" /> already established.
/// </summary>
public readonly record struct MissionZoneCommand(
    int CharacterId,
    int JoinWar,
    int KillOtherTribe,
    int KillMonster,
    int PlayTime,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    TaskCompletionSource? Applied = null);
