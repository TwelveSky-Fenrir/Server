using System.Net;
using Fenrir.Application.Login.Abstractions.Login;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.RateLimiting;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Security;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.Login;

/// <summary>
///     op11 CL_LOGIN_SEND business logic -- IP rate limit, then the application firewall, then version, then MAC
///     restriction, then auth run in that order so an over-budget/blocked/incompatible/banned-PC attempt never
///     reaches Argon2id/SQL account lookup.
/// </summary>
public sealed class LoginService(
    IAccountRepository accounts,
    IAccountPinRepository pins,
    ICharacterRepository characters,
    LoginIpRateLimiter ipRateLimiter,
    ApplicationFirewall firewall,
    IBanRepository bans,
    IMacRestrictionRepository macRestrictions,
    IOptions<LoginServerOptions> options,
    SessionRegistry registry,
    IAccountSessionRepository accountSessions) : ILoginService
{
    // Legacy tResult codes actually producible by Fenrir's flows (S04_MyWork02.cpp W_LOGIN_SEND / S08_MyDB Login):
    private const int ResultIpBlocked = 2; // MyDB::CheckGMIP/CheckBanIP (S04_MyWork02.cpp:195-215)
    private const int ResultVersionMismatch = 4; // tVersion != mServerVersion
    private const int ResultUnknownAccount = 6; // mDB.Login: account not found
    private const int ResultWrongPassword = 7; // mDB.Login: password mismatch

    /// <summary>
    ///     "Compte déjà connecté" (local login-side session, in-zone, zone-loading, or account-save-in-progress --
    ///     all four legacy cases collapse to the same tResult): ServerDocs/11_ts25login/01_Flux_Authentification_
    ///     Redirection.md:142-151,173,438,500, citing Server/ts25login/S04_MyWork02.cpp:225-284. Reused here as the
    ///     cross-process duplicate-login refusal code for runtime.AccountSessions' ConflictLogin/ConflictGameKicked/
    ///     ConflictTearingDown outcomes.
    /// </summary>
    private const int ResultAlreadyConnected = 8;

    private const int ResultBlocked = 9; // mDB.Login: uBlockInfo >= today (ban); Fenrir lockout maps here too
    private const int ResultCustomMessage = 10000; // mDB.Login: macinfo mac_limit < 1 ("Your PC has been banned.")
    private const int ResultSuccess = 0;

    private const string MacBannedMessage = "Your PC has been banned.";

    /// <summary>
    ///     Fixed reference hash so the "account not found" path pays the same Argon2id cost as a real verify
    ///     (timing-attack defense).
    /// </summary>
    private static readonly (byte[] Hash, byte[] Salt) DummyCredential =
        PasswordHasher.Hash("dummy-unused-reference-password");

    public async ValueTask<LoginResult> LoginAsync(long sessionId, IPEndPoint? remoteEndPoint, LoginRequest packet,
        CancellationToken cancellationToken)
    {
        if (!ipRateLimiter.TryConsume(remoteEndPoint))
            return LoginResult.RateLimitedResult;

        // Checked ahead of Argon2id/SQL (unlike legacy, which only ran this after a successful mDB.Login): an
        // already-known-bad IP shouldn't get a free password-guessing attempt against any account.
        if (!await firewall.IsAllowedAsync(remoteEndPoint, cancellationToken))
            return Failure(ResultIpBlocked, "", false);

        if (packet.Version != options.Value.ExpectedClientVersion)
            return Failure(ResultVersionMismatch, "", false);

        var macAddress =
            MacAddressFormatter.Format(packet.Adapter.PhysicalAddress, packet.Adapter.PhysicalAddressLength);
        if (macAddress.Length > 0 &&
            await macRestrictions.IsBannedAsync(macAddress, packet.Adapter.AdapterName, cancellationToken))
            return Failure(ResultCustomMessage, MacBannedMessage, false);

        var account = await accounts.AuthenticateAsync(packet.Id, cancellationToken);
        var result = await AuthenticateConstantTimeAsync(account, packet.Password, cancellationToken);

        if (result != ResultSuccess)
            // Re-arms VersionOk so the client can retry on this same connection without a reconnect.
            return Failure(result, "", true);

        var accountId = account!.AccountId;

        // "****" (PIN exists) vs "" (must create) is what the legacy client keys the op13/op15 choice on.
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        var requirePin = options.Value.RequireSecondPassword;
        var pinMask = storedPin is null ? "" : LoginTrain.ExistingPinMask;

        // Cross-process duplicate-login authority (runtime.AccountSessions) -- see ResultAlreadyConnected's
        // remarks for the ServerDocs citation behind tResult=8. Every successful credential check claims exactly
        // once; the claim's own atomic side effect (clear a stale row / flag a live Game row for kick / refuse a
        // tearing-down row) happens server-side in usp_AccountSession_ClaimOrSignalKick.
        var newToken = Guid.NewGuid();
        var claim = await accountSessions.ClaimOrSignalKickAsync(accountId, newToken, cancellationToken);

        switch ((AccountSessionClaimOutcome)claim.Outcome)
        {
            case AccountSessionClaimOutcome.ConflictLogin:
                // "Déjà connecté sur ts25login lui-même": kick the stale local session directly (matches the
                // legacy kick-then-tResult=8 shape -- the new attempt is refused too, not handed the account).
                if (registry.TryGetByAccount(accountId, out var existingLocal))
                {
                    existingLocal!.Abort(DisconnectReason.Evicted);
                    return LoginResult.SilentDropResult;
                }

                // Row claimed to be a live Login session but no matching local socket -- treat like any other refusal.
                return Failure(ResultAlreadyConnected, "", true);
            case AccountSessionClaimOutcome.ConflictGameKicked:
            case AccountSessionClaimOutcome.ConflictTearingDown:
                // A live Game-side session was just flagged for kick (or is mid-teardown): refuse this attempt,
                // the account isn't free yet -- the player retries once the kick/teardown completes.
                return Failure(ResultAlreadyConnected, "", true);
        }

        // Evicts any previous login session for this account still held locally (legacy playuser-result-4 local
        // kick) -- belt-and-braces alongside the cross-process claim above, since AssociateAccount's own eviction
        // only ever sees sessions on this same process.
        registry.AssociateAccount(sessionId, accountId);

        var chars = await characters.GetByAccountAsync(accountId, cancellationToken);

        return new LoginResult(LoginOutcome.Success, ResultSuccess, "", false, accountId, requirePin, pinMask,
            [..chars], newToken);
    }

    /// <summary>
    ///     Runs Argon2id verify on every branch so wall-clock time doesn't leak which failure occurred; only the returned
    ///     code differs.
    /// </summary>
    private async ValueTask<int> AuthenticateConstantTimeAsync(AuthenticateAccountDto? account, string password,
        CancellationToken ct)
    {
        if (account is null)
        {
            _ = PasswordHasher.Verify(password, DummyCredential.Hash, DummyCredential.Salt);
            return ResultUnknownAccount;
        }

        // Always awaited (not short-circuited behind account.IsBanned) so a plain, flag-only ban and a
        // ban-log-only ban (admin.Bans, never ported into auth.Accounts.IsBanned) cost the same wall-clock time.
        var loggedBan = await bans.IsActiveForAccountAsync(account.AccountId, ct);
        if (account.IsBanned || loggedBan || account.LockoutUntilUtc > DateTime.UtcNow)
        {
            // Verify still runs (outcome is fixed) so timing doesn't reveal banned/locked vs. other outcomes.
            _ = PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt);
            return ResultBlocked;
        }

        var passwordOk = PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt);

        await accounts.RecordLoginAttemptAsync(account.AccountId, passwordOk, ct);
        return passwordOk ? ResultSuccess : ResultWrongPassword;
    }

    private static LoginResult Failure(int resultCode, string resultString, bool reArmVersionOk)
    {
        return new LoginResult(LoginOutcome.Failure, resultCode, resultString, reArmVersionOk, 0, false, "", []);
    }
}
