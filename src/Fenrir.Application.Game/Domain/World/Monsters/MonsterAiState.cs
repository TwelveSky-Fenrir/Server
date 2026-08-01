namespace Fenrir.Application.Game.Domain.World.Monsters;

public enum MonsterAiState : byte
{
    Spawning = 0,

    Decision = 1,

    Patrol = 3,

    Chase = 4,

    AttackWindup = 5,

    RangedAttackWindup = 7,

    Flinch = 8,

    ReturnToSpawn = 19,

    Dead = 12
}
