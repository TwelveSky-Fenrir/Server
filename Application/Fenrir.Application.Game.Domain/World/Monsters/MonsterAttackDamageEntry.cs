namespace Fenrir.Application.Game.Domain.World.Monsters;

internal sealed class MonsterAttackDamageEntry
{
    public required int CharacterId { get; init; }
    public required object SessionToken { get; init; }
    public long CumulativeDamage { get; set; }
}
