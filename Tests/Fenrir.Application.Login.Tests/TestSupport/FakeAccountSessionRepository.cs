using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IAccountSessionRepository: defaults to always claiming successfully (Registered), the
// path every existing LoginService test exercises. Tests of the duplicate-login conflict branches configure
// ClaimOutcome/PreviousShardId explicitly.
internal sealed class FakeAccountSessionRepository : IAccountSessionRepository
{
    public AccountSessionClaimOutcome ClaimOutcome { get; set; } = AccountSessionClaimOutcome.Registered;
    public byte? PreviousShardId { get; set; }
    public bool TransitionResult { get; set; } = true;

    public ImmutableArray<ReapedAccountSessionDto> ReapResult { get; set; } = ImmutableArray<ReapedAccountSessionDto>.Empty;

    public int ClaimCallCount { get; private set; }
    public int? LastTearingDownAccountId { get; private set; }
    public (int AccountId, AccountSessionServerKind ServerKind, byte? ShardId, Guid SessionToken)? LastClearedOwner
    {
        get;
        private set;
    }

    public List<(AccountSessionServerKind ServerKind, byte? ShardId, IReadOnlyCollection<int> AccountIds)>
        RefreshCalls { get; } = [];

    public ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
        CancellationToken ct)
    {
        ClaimCallCount++;
        return ValueTask.FromResult(new AccountSessionClaimDto((byte)ClaimOutcome, PreviousShardId));
    }

    public ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
        CancellationToken ct)
    {
        return ValueTask.FromResult(TransitionResult);
    }

    public ValueTask MarkTearingDownAsync(int accountId, CancellationToken ct)
    {
        LastTearingDownAccountId = accountId;
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearIfOwnerAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct)
    {
        LastClearedOwner = (accountId, serverKind, shardId, sessionToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<KickedAccountDto>> RefreshAndGetKickedAsync(AccountSessionServerKind serverKind,
        byte? shardId, IReadOnlyCollection<int> accountIds, CancellationToken ct)
    {
        RefreshCalls.Add((serverKind, shardId, accountIds));
        return ValueTask.FromResult(ImmutableArray<KickedAccountDto>.Empty);
    }

    public ValueTask<ImmutableArray<ReapedAccountSessionDto>> ReapStaleAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(ReapResult);
    }

    public int ActiveSessionCount { get; set; }
    public int ActiveSessionCountCallCount { get; private set; }
    public Exception? ActiveSessionCountException { get; set; }

    public ValueTask<int> GetActiveSessionCountAsync(CancellationToken ct)
    {
        ActiveSessionCountCallCount++;
        return ActiveSessionCountException is null
            ? ValueTask.FromResult(ActiveSessionCount)
            : throw ActiveSessionCountException;
    }
}
