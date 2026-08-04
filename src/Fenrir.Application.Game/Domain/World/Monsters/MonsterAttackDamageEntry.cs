using Fenrir.Application.Game.Domain.World.Runtime;

namespace Fenrir.Application.Game.Domain.World.Monsters;

internal sealed class MonsterAttackDamageEntry
{
    public required int CharacterId { get; init; }
    public required RuntimeIncarnation Incarnation { get; init; }
    public long CumulativeDamage { get; set; }
}
