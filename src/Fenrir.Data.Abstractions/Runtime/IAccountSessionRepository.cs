using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Runtime;

public enum AccountSessionServerKind : byte
{
    Login = 0,
    Game = 1
}

public enum AccountSessionClaimOutcome : byte
{
    Registered = 0,
    ConflictLogin = 1,
    ConflictGameKicked = 2,
    ConflictTearingDown = 3,

    ReclaimedDeadShard = 4
}

public interface IAccountSessionRepository
{
    public ValueTask<AccountSessionClaimDto> ClaimOrSignalKickAsync(int accountId, Guid newSessionToken,
        CancellationToken ct);

    public ValueTask<bool> TransitionToGameAsync(int accountId, Guid expectedSessionToken, byte shardId,
        CancellationToken ct);

    public ValueTask MarkTearingDownAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct);

    public ValueTask ClearIfOwnerAsync(int accountId, AccountSessionServerKind serverKind, byte? shardId,
        Guid sessionToken, CancellationToken ct);

    public ValueTask<ImmutableArray<KickedAccountDto>> RefreshAndGetKickedAsync(AccountSessionServerKind serverKind,
        byte? shardId, IReadOnlyCollection<int> accountIds, CancellationToken ct);

    // Renvoie les baux effectivement detenus : tout couple envoye et non renvoye a perdu sa ligne.
    // Server/ts25login/S07_MyGame01.cpp:59-72 (register_2 != 0 -> Quit de la session).
    public ValueTask<ImmutableArray<HeldAccountSessionDto>> RefreshAndGetHeldLeasesAsync(
        AccountSessionServerKind serverKind, byte? shardId, IReadOnlyCollection<AccountSessionLeaseTvp> leases,
        CancellationToken ct);

    public ValueTask<ImmutableArray<ReapedAccountSessionDto>> ReapStaleAsync(CancellationToken ct);

    public ValueTask<int> GetActiveSessionCountAsync(CancellationToken ct);

    public ValueTask<int> GetConcurrentDeviceSessionCountAsync(int excludingAccountId, string adapterIdentifier,
        string localIp, string remoteIp, CancellationToken ct);

    public ValueTask RecordDeviceSignatureAsync(int accountId, string adapterIdentifier, string localIp,
        string remoteIp, CancellationToken ct);
}
