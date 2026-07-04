using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Quests;

/// <summary>
///     Posted after a quest-state transition is already durably persisted; Zone's tick mirrors it onto
///     PlayerRuntimeState.
/// </summary>
/// <param name="ExperienceDelta">
///     Flat add to Experience -- the reward value IS the gain, unlike a monster kill's level-gap
///     formula.
/// </param>
/// <param name="Containers">Populated only when the transition touched an inventory container; empty otherwise.</param>
/// <param name="Applied">
///     Completed the instant the tick mirrors this -- callers must await it before releasing
///     PlayerRuntimeState.EconomyActionLock, so a back-to-back action never reads a stale snapshot.
/// </param>
public readonly record struct QuestZoneCommand(
    int CharacterId,
    QuestProgress Progress,
    int ExperienceDelta,
    int ContributionPointsDelta,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    TaskCompletionSource? Applied = null,
    int TeacherPointDelta = 0);
