using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for ISessionTicketRepository: ConsumeAsync/PurgeExpiredAsync are GameServer/maintenance
// concerns, never exercised from the Login-side handoff handler under test here.
internal sealed class FakeSessionTicketRepository : ISessionTicketRepository
{
    public (int AccountId, int CharacterId, byte ShardId, int TtlSeconds, Guid SessionToken, short AccountGrade)?
        LastCreatedTicket
    {
        get;
        private set;
    }

    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, Guid sessionToken,
        short accountGrade, CancellationToken ct)
    {
        LastCreatedTicket = (accountId, characterId, shardId, ttlSeconds, sessionToken, accountGrade);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask PurgeExpiredAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
