using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.LoginServer.Tests.TestSupport;

// In-memory stand-in for IAccountSessionRepository: never exercised by BuildGreetingPacket itself, only
// needed to satisfy LoginConnectionHost's constructor (LoginCapacityState -- not this repository -- is what
// GreetingCapacityPacketTests actually drives).
internal sealed class FakeAccountSessionRepository : IAccountSessionRepository
{
    public ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
        CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask MarkTearingDownAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask ClearIfOwnerAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ImmutableArray<KickedAccountDto>> RefreshAndGetKickedAsync(AccountSessionServerKind serverKind,
        byte? shardId, IReadOnlyCollection<int> accountIds, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<ImmutableArray<ReapedAccountSessionDto>> ReapStaleAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> GetActiveSessionCountAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
