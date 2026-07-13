using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Gm;

public readonly record struct GmZone124PartyPullZoneCommand(
    int TargetCharacterId,
    string TargetPartyName,
    float DestinationX,
    float DestinationY,
    float DestinationZ,
    TaskCompletionSource<IReadOnlyList<PlayerRuntimeState>>? Applied = null);
