using Fenrir.Application.Login.RateLimiting;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Domain.Security;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     CL_LOGIN_SEND (op11) — account authentication and character-list handoff (wire contract §4.2-§4.4, login
///     protocol report §4.11). Order of checks matters:
///     <list type="number">
///         <item>
///             The per-IP <see cref="LoginIpRateLimiter" /> is consulted BEFORE anything else — including the
///             version check — because it is the cheapest possible guard (a dictionary lookup) and the one thing
///             that survives a bruteforcer simply reconnecting TCP between attempts (the per-session
///             <c>SessionRateLimiter</c> does not: a new connection means a fresh <c>SessionId</c> and a fresh,
///             fully-topped-up bucket). Letting an over-budget attempt reach SQL or Argon2id first would defeat the
///             point of having an IP-keyed limiter at all.
///         </item>
///         <item>Client version is checked next — still no DB round-trip for a client that cannot possibly be compatible.</item>
///         <item>
///             Authentication runs at approximately constant time regardless of outcome (unknown login, wrong
///             password, banned and locked-out all funnel through a similarly-costed Argon2id verify), but the
///             REPLY carries the real legacy result codes the EU33 client renders — 6 (unknown account),
///             7 (wrong password), 9 (blocked), 4 (version) — instead of M1's anti-enumeration blanket 1
///             (plan: "codes résultat legacy réels"). The timing side-channel stays closed; the code channel is
///             deliberately legacy-faithful.
///         </item>
///         <item>
///             Success or failure, the reply is the FULL legacy train — LC_LOGIN_RECV + 3× LC_USER_AVATAR_RECV2 +
///             LC_RECOMMAND_WORLD_RECV + LC_RECOMMAND_WORLD2_RECV — via <see cref="LoginTrain" /> (SEND_LOGIN,
///             S04_MyWork02.cpp l.42-67 sends it in every DO_SEND path).
///         </item>
///         <item>
///             On success the one-connection-per-account invariant is enforced via
///             <see cref="SessionRegistry.AssociateAccount" />: a previous login session holding the account is
///             evicted, the legacy equivalent of the playuser-result-4 local kick (<c>mUSER[i]->Quit()</c>). The
///             legacy then replies NOTHING and lets the client retry; Fenrir proceeds with the fresh login
///             directly — one fewer round-trip, same end state, same invariant. Result 8 ("account in use" on a
///             ZONE) has no cross-process source yet — see the chantier's openIssues.
///         </item>
///         <item>
///             P2ndPassword=1 (prod EU33): when <see cref="LoginServerOptions.RequireSecondPassword" /> is true the
///             reply carries <c>tSecondLoginSort=1</c> and the session parks in <c>PinRequired</c> — the client then
///             shows the PIN screen and sends op 13 (no PIN stored: <c>tMousePassword=""</c>) or op 15 (PIN stored:
///             <c>"****"</c>). With it off, the session goes straight to the legacy <c>mSecondLoginSort=0</c> path.
///         </item>
///     </list>
/// </summary>
public sealed class LoginHandler(
    IAccountRepository accounts,
    IAccountPinRepository pins,
    ICharacterRepository characters,
    LoginIpRateLimiter ipRateLimiter,
    IOptions<LoginServerOptions> options,
    SessionRegistry registry) : IAsyncPacketHandler<LoginRequest>
{
    // Legacy tResult codes actually producible by Fenrir's flows (S04_MyWork02.cpp W_LOGIN_SEND / S08_MyDB Login):
    private const int ResultVersionMismatch = 4; // tVersion != mServerVersion
    private const int ResultUnknownAccount = 6; // mDB.Login: account not found
    private const int ResultWrongPassword = 7; // mDB.Login: password mismatch
    private const int ResultBlocked = 9; // mDB.Login: uBlockInfo >= today (ban); Fenrir lockout maps here too
    private const int ResultSuccess = 0;

    /// <summary>
    ///     Fixed reference hash/salt, computed once at type load, purely so the "account not found" path pays an
    ///     Argon2id cost roughly equal to a real verify's (see the timing note on <see cref="HandleAsync" />).
    ///     The password hashed here is never compared against anything real — only its CPU cost matters.
    /// </summary>
    private static readonly (byte[] Hash, byte[] Salt) DummyCredential =
        PasswordHasher.Hash("dummy-unused-reference-password");

    public async ValueTask HandleAsync(LoginRequest packet, IPacketSession session, CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        // Silent drop, no state change: see the class doc for why this must run before anything else, and why it
        // neither replies nor aborts the connection — a legitimate client that merely burst its IP-wide budget
        // (e.g. sharing a NAT) just retries later on the very same connection.
        if (!ipRateLimiter.TryConsume(loginSession.RemoteEndPoint))
            return;

        if (packet.Version != options.Value.ExpectedClientVersion)
        {
            LoginTrain.SendFailure(session, ResultVersionMismatch, packet.Id);
            return;
        }

        var account = await accounts.AuthenticateAsync(packet.Id, cancellationToken);
        var result = await AuthenticateConstantTimeAsync(account, packet.Password, cancellationToken);

        if (result != ResultSuccess)
        {
            // LoginRequest.AllowedStates = [Connected, VersionOk]: re-arming VersionOk lets the client retry on
            // this same connection without a reconnect. The failure train still echoes the client's own tID,
            // sort=0, loginSort=0, PIN "0000" — byte-for-byte the legacy DO_SEND failure shape.
            loginSession.MarkVersionOk();
            LoginTrain.SendFailure(session, result, packet.Id);
            return;
        }

        // result == ResultSuccess only when AuthenticateConstantTimeAsync took the "account found" branch.
        var accountId = account!.AccountId;

        // "****" when a PIN exists (the real PIN never travels on login), "" when the client must create one —
        // this is what the legacy client keys the op 13 (create) vs op 15 (verify) choice on (report §5.11).
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        var requirePin = options.Value.RequireSecondPassword;
        var secondLoginSort = requirePin ? 1 : 0;
        var pinMask = storedPin is null ? "" : LoginTrain.ExistingPinMask;

        loginSession.MarkAuthenticated(accountId);
        if (requirePin)
            loginSession.MarkPinRequired();

        // Evicts (aborts) any previous login session for this account — the legacy playuser-result-4 local kick.
        // Unlike the legacy we then proceed instead of replying nothing; see the class doc, point 5.
        registry.AssociateAccount(loginSession.SessionId, accountId);

        var chars = await characters.GetByAccountAsync(accountId, cancellationToken);
        LoginTrain.Send(session,
            LoginTrain.BuildLoginRecv(ResultSuccess, "MG" + accountId, secondLoginSort, pinMask),
            LoginTrain.BuildAvatarSlots(chars));
    }

    /// <summary>
    ///     Runs an Argon2id verify unconditionally down every branch, so wall-clock time is dominated by the same
    ///     expensive hash regardless of outcome; only the returned legacy code differs. Known, accepted residual:
    ///     the "unknown login" branch skips the <see cref="AccountRepository.RecordLoginAttemptAsync" /> SQL
    ///     round-trip that the "known account" branch pays below (there is no AccountId to record against) — a
    ///     single round-trip is negligible next to Argon2id's tens-of-milliseconds cost.
    /// </summary>
    private async ValueTask<int> AuthenticateConstantTimeAsync(AuthenticateAccountDto? account, string password,
        CancellationToken ct)
    {
        if (account is null)
        {
            _ = PasswordHasher.Verify(password, DummyCredential.Hash, DummyCredential.Salt);
            return ResultUnknownAccount;
        }

        if (account.IsBanned || account.LockoutUntilUtc > DateTime.UtcNow)
        {
            // Outcome is fixed regardless of the password, but Verify must still run (against the real, already
            // fetched hash/salt — no extra DB cost) so this branch pays the exact same Argon2id latency as every
            // other branch. Skipping it would let a network observer distinguish "banned/locked" from
            // "unknown login" or "wrong password" by response time alone.
            _ = PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt);
            return ResultBlocked;
        }

        var passwordOk = PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt);

        // Only the "account exists, not banned, not locked" branch reaches here, so this is exactly the
        // wrong-password-vs-success signal usp_Account_RecordLoginAttempt exists to track (lockout escalation on
        // failure, counter reset on success) — never called for an unknown/banned/locked-out account.
        await accounts.RecordLoginAttemptAsync(account.AccountId, passwordOk, ct);
        return passwordOk ? ResultSuccess : ResultWrongPassword;
    }
}
