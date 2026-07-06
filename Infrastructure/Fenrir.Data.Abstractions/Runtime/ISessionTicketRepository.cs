namespace Fenrir.Data.Abstractions.Runtime;

public interface ISessionTicketRepository
{
    /// <summary>
    ///     sessionToken is the cross-process AccountSession token minted at Login-claim time
    ///     (usp_AccountSession_ClaimOrSignalKick); carried through so usp_AccountSession_TransitionToGame can verify the
    ///     Game-side world-entry claim is for the same login epoch. accountGrade is the Login-side account-grade fact
    ///     (legacy uUserSort, LoginClientSession.AccountGrade); carried through so the Zone session can gate GM-only
    ///     commands without ever re-querying auth.Accounts itself (Migrations/012_session_tickets_add_account_grade.sql).
    /// </summary>
    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, Guid sessionToken,
        short accountGrade, CancellationToken ct);

    public ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, CancellationToken ct);

    public ValueTask PurgeExpiredAsync(CancellationToken ct);
}
