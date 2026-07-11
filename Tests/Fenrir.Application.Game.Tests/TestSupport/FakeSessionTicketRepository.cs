using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeSessionTicketRepository : ISessionTicketRepository
{
    public ConsumedTicketDto? TicketToReturn { get; set; }

    public List<(int AccountId, int CharacterId, byte ShardId, int TtlSeconds, Guid SessionToken, short AccountGrade)>
        CreatedTickets { get; } = [];

    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, Guid sessionToken,
        short accountGrade, CancellationToken ct)
    {
        CreatedTickets.Add((accountId, characterId, shardId, ttlSeconds, sessionToken, accountGrade));
        return ValueTask.CompletedTask;
    }

    public ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, CancellationToken ct)
    {
        return ValueTask.FromResult(TicketToReturn);
    }

    public ValueTask PurgeExpiredAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
