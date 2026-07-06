using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.TestSupport;

// In-memory stand-in for IAccountSessionRepository: defaults to TransitionToGameAsync succeeding (the path
// every pre-existing ZoneHandshake flow exercises) and RefreshAndGetKickedAsync returning a scripted set of
// kicked accounts for AccountSessionKickPollHostTests to assert against.
internal sealed class FakeAccountSessionRepository : IAccountSessionRepository
{
    public bool TransitionResult { get; set; } = true;
    public ImmutableArray<KickedAccountDto> KickedAccounts { get; set; } = ImmutableArray<KickedAccountDto>.Empty;

    public (int AccountId, Guid ExpectedSessionToken, byte ShardId)? LastTransition { get; private set; }
    public List<int> TearingDownAccountIds { get; } = [];

    public List<(int AccountId, AccountSessionServerKind ServerKind, byte? ShardId, Guid SessionToken)>
        TearingDownOwners { get; } = [];

    public List<(int AccountId, AccountSessionServerKind ServerKind, byte? ShardId, Guid SessionToken)>
        ClearedOwners { get; } = [];

    public List<(AccountSessionServerKind ServerKind, byte? ShardId, IReadOnlyCollection<int> AccountIds)>
        RefreshCalls { get; } = [];

    public ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
        CancellationToken ct)
    {
        throw new NotSupportedException("Claiming happens only on the Login side.");
    }

    public ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
        CancellationToken ct)
    {
        LastTransition = (accountId, expectedSessionToken, shardId);
        return ValueTask.FromResult(TransitionResult);
    }

    public ValueTask MarkTearingDownAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct)
    {
        TearingDownAccountIds.Add(accountId);
        TearingDownOwners.Add((accountId, serverKind, shardId, sessionToken));
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearIfOwnerAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct)
    {
        ClearedOwners.Add((accountId, serverKind, shardId, sessionToken));
        return ValueTask.CompletedTask;
    }

    public ValueTask<ImmutableArray<KickedAccountDto>> RefreshAndGetKickedAsync(AccountSessionServerKind serverKind,
        byte? shardId, IReadOnlyCollection<int> accountIds, CancellationToken ct)
    {
        RefreshCalls.Add((serverKind, shardId, accountIds));
        return ValueTask.FromResult(KickedAccounts);
    }

    public ValueTask<ImmutableArray<ReapedAccountSessionDto>> ReapStaleAsync(CancellationToken ct)
    {
        throw new NotSupportedException("Reaping runs only from the single unsharded Login-side timer.");
    }

    public ValueTask<int> GetActiveSessionCountAsync(CancellationToken ct)
    {
        throw new NotSupportedException("Not exercised by the Game-side test suites in this project.");
    }
}
