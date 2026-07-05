using System.Collections.Immutable;
using System.Net;
using Fenrir.Application.Login.RateLimiting;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Data.Characters;
using Fenrir.Data.Security;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Handlers.Services;

public enum LoginOutcome
{
    /// <summary>Silent drop, no reply/abort: a legitimate NAT-shared client that burst its IP budget just retries later.</summary>
    RateLimited,
    Failure,
    Success
}

/// <summary>
///     <paramref name="ReArmVersionOk" /> mirrors the legacy quirk where only a full authentication attempt
///     (account lookup + password verify) re-arms VersionOk so the client can retry on the same connection --
///     the earlier gates (IP block/version/MAC ban) never re-arm it.
/// </summary>
public sealed record LoginResult(
    LoginOutcome Outcome,
    int ResultCode,
    string ResultString,
    bool ReArmVersionOk,
    int AccountId,
    bool RequirePin,
    string PinMask,
    ImmutableArray<CharacterSummaryDto> Characters)
{
    public static LoginResult RateLimitedResult { get; } =
        new(LoginOutcome.RateLimited, 0, "", false, 0, false, "", []);
}

public interface ILoginService
{
    ValueTask<LoginResult> LoginAsync(long sessionId, IPEndPoint? remoteEndPoint, LoginRequest packet,
        CancellationToken cancellationToken);
}

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
    SessionRegistry registry) : ILoginService
{
    // Legacy tResult codes actually producible by Fenrir's flows (S04_MyWork02.cpp W_LOGIN_SEND / S08_MyDB Login):
    private const int ResultIpBlocked = 2; // MyDB::CheckGMIP/CheckBanIP (S04_MyWork02.cpp:195-215)
    private const int ResultVersionMismatch = 4; // tVersion != mServerVersion
    private const int ResultUnknownAccount = 6; // mDB.Login: account not found
    private const int ResultWrongPassword = 7; // mDB.Login: password mismatch
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

        // Evicts any previous login session for this account (legacy playuser-result-4 local kick), then proceeds.
        registry.AssociateAccount(sessionId, accountId);

        var chars = await characters.GetByAccountAsync(accountId, cancellationToken);

        return new LoginResult(LoginOutcome.Success, ResultSuccess, "", false, accountId, requirePin, pinMask,
            [..chars]);
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
