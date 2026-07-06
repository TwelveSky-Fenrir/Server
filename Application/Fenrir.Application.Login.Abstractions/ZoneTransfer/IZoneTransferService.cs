namespace Fenrir.Application.Login.Abstractions.ZoneTransfer;

public enum ZoneTransferOutcome
{
    CharacterNotFound,
    ShardUnavailable,
    Success
}

public readonly record struct ZoneTransferResult(ZoneTransferOutcome Outcome, string Ip, int Port, short Zone);

public interface IZoneTransferService
{
    /// <param name="sessionToken">
    ///     The <c>runtime.AccountSessions</c> token minted at Login-claim time (<c>LoginClientSession.AccountSessionToken</c>,
    ///     set by <c>usp_AccountSession_ClaimOrSignalKick</c>); carried into the minted <c>SessionTicket</c> so
    ///     <c>usp_AccountSession_TransitionToGame</c> can verify the Game-side world-entry claim is for the same
    ///     login epoch.
    /// </param>
    /// <param name="accountGrade">
    ///     <c>LoginClientSession.AccountGrade</c> (legacy <c>uUserSort</c>); carried into the minted
    ///     <c>SessionTicket</c> so the Zone session inherits the GM-elevation fact without ever re-querying
    ///     auth.Accounts itself.
    /// </param>
    public ValueTask<ZoneTransferResult> RequestZoneTransferAsync(int accountId, byte avatarPost, Guid sessionToken,
        short accountGrade, CancellationToken cancellationToken);
}
