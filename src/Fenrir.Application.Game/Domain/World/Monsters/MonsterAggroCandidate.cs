using Fenrir.Application.Game.Domain.World.Runtime;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public readonly record struct MonsterAggroCandidate(
    int CharacterId,
    RuntimeIncarnation Incarnation,
    uint UniqueNumber,
    float DistanceSquared,
    float PosX,
    float PosY,
    float PosZ);
