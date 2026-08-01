using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Progression;

public readonly record struct MissionZoneCommand(
    int CharacterId,
    int JoinWar,
    int KillOtherTribe,
    int KillMonster,
    int PlayTime,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    TaskCompletionSource? Applied = null);
