using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Quests;

public readonly record struct QuestZoneCommand(
    int CharacterId,
    QuestProgress Progress,
    int ExperienceDelta,
    int KillOtherTribeCountDelta,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    TaskCompletionSource? Applied = null,
    int TeacherPointDelta = 0);
