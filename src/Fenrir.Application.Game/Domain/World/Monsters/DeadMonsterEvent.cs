namespace Fenrir.Application.Game.Domain.World.Monsters;

public sealed record DeadMonsterEvent(
    MonsterEntity Monster,
    int? AttackerCharacterId,
    int? CreditedCharacterId,
    TimeSpan DiedAtZoneClock,
    DateTime DiedAtUtc);
