using Fenrir.Application.Login.RateLimiting;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Security;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Handlers;

/// <summary>op11 CL_LOGIN_SEND — IP rate limit, then version, then auth run in that order so an over-budget/incompatible attempt never reaches Argon2id/SQL.</summary>
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

    /// <summary>Fixed reference hash so the "account not found" path pays the same Argon2id cost as a real verify (timing-attack defense).</summary>
    private static readonly (byte[] Hash, byte[] Salt) DummyCredential =
        PasswordHasher.Hash("dummy-unused-reference-password");

    public async ValueTask HandleAsync(LoginRequest packet, IPacketSession session, CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        // Silent drop, no reply/abort: a legitimate NAT-shared client that burst its IP budget just retries later.
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
            // Re-arms VersionOk so the client can retry on this same connection without a reconnect.
            loginSession.MarkVersionOk();
            LoginTrain.SendFailure(session, result, packet.Id);
            return;
        }

        var accountId = account!.AccountId;

        // "****" (PIN exists) vs "" (must create) is what the legacy client keys the op13/op15 choice on.
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        var requirePin = options.Value.RequireSecondPassword;
        var secondLoginSort = requirePin ? 1 : 0;
        var pinMask = storedPin is null ? "" : LoginTrain.ExistingPinMask;

        loginSession.MarkAuthenticated(accountId);
        if (requirePin)
            loginSession.MarkPinRequired();

        // Evicts any previous login session for this account (legacy playuser-result-4 local kick), then proceeds.
        registry.AssociateAccount(loginSession.SessionId, accountId);

        var chars = await characters.GetByAccountAsync(accountId, cancellationToken);
        LoginTrain.Send(session,
            LoginTrain.BuildLoginRecv(ResultSuccess, "MG" + accountId, secondLoginSort, pinMask),
            LoginTrain.BuildAvatarSlots(chars));
    }

    /// <summary>Runs Argon2id verify on every branch so wall-clock time doesn't leak which failure occurred; only the returned code differs.</summary>
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
            // Verify still runs (outcome is fixed) so timing doesn't reveal banned/locked vs. other outcomes.
            _ = PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt);
            return ResultBlocked;
        }

        var passwordOk = PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt);

        await accounts.RecordLoginAttemptAsync(account.AccountId, passwordOk, ct);
        return passwordOk ? ResultSuccess : ResultWrongPassword;
    }
}
