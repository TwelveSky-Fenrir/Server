using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

// In-memory stand-in for ISessionTicketRepository: CreateAsync/PurgeExpiredAsync are Login-side/maintenance
// concerns, never exercised from ZoneHandshakeService under test here.
internal sealed class FakeSessionTicketRepository : ISessionTicketRepository
{
    public ConsumedTicketDto? TicketToReturn { get; set; }

    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, Guid sessionToken,
        short accountGrade, CancellationToken ct)
    {
        throw new NotSupportedException();
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
