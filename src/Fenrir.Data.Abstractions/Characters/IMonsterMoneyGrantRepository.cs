namespace Fenrir.Data.Abstractions.Characters;

public interface IMonsterMoneyGrantRepository
{
    public ValueTask<MonsterMoneyGrantResultDto> ApplyIdempotentAsync(
        Guid correlationId,
        int characterId,
        long amount,
        CancellationToken ct);
}
