namespace Fenrir.Application.Game.Domain.World.Monsters;

public readonly record struct MonsterAggroCandidate(
    int CharacterId,
    uint UniqueNumber,
    float DistanceSquared,
    float PosX,
    float PosY,
    float PosZ);
