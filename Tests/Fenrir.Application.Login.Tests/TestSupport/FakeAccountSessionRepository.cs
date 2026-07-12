using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeAccountSessionRepository : IAccountSessionRepository
{
    public AccountSessionClaimOutcome ClaimOutcome { get; set; } = AccountSessionClaimOutcome.Registered;
    public byte? PreviousShardId { get; set; }
    public bool TransitionResult { get; set; } = true;

    public SqlException? ClaimException { get; set; }

    public ImmutableArray<ReapedAccountSessionDto> ReapResult { get; set; } =
        ImmutableArray<ReapedAccountSessionDto>.Empty;

    public int ClaimCallCount { get; private set; }
    public int? LastTearingDownAccountId { get; private set; }

    public (int AccountId, AccountSessionServerKind ServerKind, byte? ShardId, Guid SessionToken)?
        LastTearingDownOwner { get; private set; }

    public (int AccountId, AccountSessionServerKind ServerKind, byte? ShardId, Guid SessionToken)? LastClearedOwner
    {
        get;
        private set;
    }

    public List<(AccountSessionServerKind ServerKind, byte? ShardId, IReadOnlyCollection<int> AccountIds)>
        RefreshCalls { get; } = [];

    public int ActiveSessionCount { get; set; }
    public int ActiveSessionCountCallCount { get; private set; }
    public Exception? ActiveSessionCountException { get; set; }

    public int ConcurrentDeviceSessionCount { get; set; }

    public (int ExcludingAccountId, string AdapterIdentifier, string LocalIp, string RemoteIp)?
        LastConcurrentDeviceSessionQuery { get; private set; }

    public (int AccountId, string AdapterIdentifier, string LocalIp, string RemoteIp)? LastRecordedDeviceSignature
    {
        get;
        private set;
    }

    public ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
        CancellationToken ct)
    {
        ClaimCallCount++;
        return ClaimException is null
            ? ValueTask.FromResult(new AccountSessionClaimDto((byte)ClaimOutcome, PreviousShardId))
            : throw ClaimException;
    }

    public ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
        CancellationToken ct)
    {
        return ValueTask.FromResult(TransitionResult);
    }

    public ValueTask MarkTearingDownAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct)
    {
        LastTearingDownAccountId = accountId;
        LastTearingDownOwner = (accountId, serverKind, shardId, sessionToken);
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

    public ValueTask<int> GetActiveSessionCountAsync(CancellationToken ct)
    {
        ActiveSessionCountCallCount++;
        return ActiveSessionCountException is null
            ? ValueTask.FromResult(ActiveSessionCount)
            : throw ActiveSessionCountException;
    }

    public ValueTask<int> GetConcurrentDeviceSessionCountAsync(int excludingAccountId, string adapterIdentifier,
        string localIp, string remoteIp, CancellationToken ct)
    {
        LastConcurrentDeviceSessionQuery = (excludingAccountId, adapterIdentifier, localIp, remoteIp);
        return ValueTask.FromResult(ConcurrentDeviceSessionCount);
    }

    public ValueTask RecordDeviceSignatureAsync(int accountId, string adapterIdentifier, string localIp,
        string remoteIp, CancellationToken ct)
    {
        LastRecordedDeviceSignature = (accountId, adapterIdentifier, localIp, remoteIp);
        return ValueTask.CompletedTask;
    }
}
