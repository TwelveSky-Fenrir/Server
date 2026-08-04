namespace Fenrir.Application.Game.Domain.World;

public enum MoneyGrantPersistenceMode
{
    CharacterAdjustment,
    MonsterLootIdempotent
}

public readonly record struct PendingMoneyGrant(
    int CharacterId,
    long Amount,
    MoneyGrantPersistenceMode PersistenceMode,
    Guid CorrelationId = default);
