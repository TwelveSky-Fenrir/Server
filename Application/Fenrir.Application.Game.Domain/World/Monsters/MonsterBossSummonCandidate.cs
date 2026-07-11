namespace Fenrir.Application.Game.Domain.World.Monsters;

public readonly record struct MonsterBossSummonCandidate(
    int MonsterId,
    float X,
    float Y,
    float Z,
    float Radius,
    int Number);

public enum MonsterBossSummonState : byte
{

        Reload = 1,

        Check = 2,

        Wait = 3
}
