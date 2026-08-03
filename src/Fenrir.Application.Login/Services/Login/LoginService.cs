using System.Collections.Immutable;
using System.Net;
using CaeriusNet.Exceptions;
using Fenrir.Application.Login.Abstractions.Login;
using Fenrir.Application.Login.Abstractions.RetiredItems;
using Fenrir.Application.Login.Services.AccountSecurity;
using Fenrir.Domain.Login;
using Fenrir.Domain.Login.Avatars;
using Fenrir.Domain.Login.Security;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Login;
using Fenrir.Security;
using Fenrir.Security.Credentials;
using Fenrir.Security.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.Login;

public sealed class LoginService(
    IAccountRepository accounts,
    IAccountPinRepository pins,
    ICharacterRepository characters,
    LoginIpRateLimiter ipRateLimiter,
    LoginCapacityState capacity,
    ApplicationFirewall firewall,
    IGmAllowlistRepository gmAllowlist,
    IBanRepository bans,
    IMacRestrictionRepository macRestrictions,
    IOptions<LoginServerOptions> options,
    SessionRegistry registry,
    IAccountSessionRepository accountSessions,
    IRetiredItemPurgeService retiredItems,
    IEventLogRepository eventLog,
    IGuildRepository guilds,
    IFriendRepository friends,
    IMentorRepository mentor,
    ILogger<LoginService> logger) : ILoginService
{
    private const int ResultMaintenance = 1;

    private const int ResultIpBlocked = 2;
    private const int ResultServerFull = 3;
    private const int ResultVersionMismatch = 4;

    private const int ResultSessionRegistrationFailed = 5;

    private const int ResultUnknownAccount = 6;
    private const int ResultWrongPassword = 7;

    private const int ResultAlreadyConnected = 8;

    private const int ResultBlocked = 9;

    private const int ResultAvatarLoadFailed = 10;

    private const int ResultCustomMessage = 10000;

    private const int ResultSuccess = 0;

    private const string MacBannedMessage = "Your PC has been banned.";
    private const string InvalidDevicesMessage = "Invalid devices!";
    private const string OnlyAdminMessage = "Only Admin can login";
    private const string ConnectionLimitExceededMessage = "The connection limit from your PC has been exceeded.";

    private const string AdapterNameEmptyMessage = "IP address not specified. Please update to the latest client.";
    private const string AccountLockedOutMessage = "Too many failed login attempts. Please try again later.";
    private const string RateLimitedMessage = "Too many login attempts from your IP. Please try again later.";

    private const short LoginSucceededEventCode = 1;

    private static readonly (byte[] Hash, byte[] Salt) DummyCredential =
        PasswordHasher.Hash("dummy-unused-reference-password");

    public async ValueTask<LoginResult> LoginAsync(long sessionId, IPEndPoint? remoteEndPoint, LoginRequest packet,
        CancellationToken cancellationToken)
    {
        if (!ipRateLimiter.TryConsume(remoteEndPoint))
        {
            logger.LogWarning("Login rejected: IP {RemoteIp} exceeded its rate-limit budget", remoteEndPoint);
            return Failure(ResultCustomMessage, RateLimitedMessage, false);
        }

        if (!await firewall.IsAllowedAsync(remoteEndPoint, cancellationToken))
        {
            logger.LogWarning("Login rejected: IP {RemoteIp} is blocked by the application firewall", remoteEndPoint);
            return Failure(ResultIpBlocked, "", true);
        }

        switch (capacity.TryReserveSlot())
        {
            case LoginCapacityOutcome.Maintenance:
                logger.LogWarning("Login rejected: server is under maintenance (login {Id})", packet.Id);
                return Failure(ResultMaintenance, "", false);
            case LoginCapacityOutcome.ServerFull:
                logger.LogWarning("Login rejected: server is full (login {Id})", packet.Id);
                return Failure(ResultServerFull, "", false);
        }

        var reservationConsumed = false;

        try
        {
            if (packet.Version != options.Value.ExpectedClientVersion)
            {
                logger.LogWarning("Login rejected: client version mismatch (login {Id}, version {Version})", packet.Id,
                    packet.Version);
                return Failure(ResultVersionMismatch, "", false);
            }

            var macAddress =
                MacAddressFormatter.Format(packet.Adapter.PhysicalAddress, packet.Adapter.PhysicalAddressLength);

            AuthenticateAccountDto? account;
            try
            {
                account = await accounts.AuthenticateAsync(packet.Id, cancellationToken);
            }
            catch (CaeriusNetSqlException ex)
            {
                logger.LogError(ex, "Login failed: account lookup errored for login {Id}", packet.Id);
                return Failure(ResultUnknownAccount, "", true);
            }

            var remoteIp = remoteEndPoint?.Address.ToString();

            if (account is not null && account.AccountGrade >= DeviceSpoofingGuard.GmGradeThreshold &&
                remoteIp is not null && !await IsGmLoginIpAllowedAsync(remoteIp, cancellationToken))
            {
                _ = PasswordHasher.Verify(packet.Password, account.PasswordHash, account.PasswordSalt);
                logger.LogWarning(
                    "Login rejected: GM-tier account {AccountId} attempted login from non-allowlisted IP {RemoteIp}",
                    account.AccountId, remoteIp);
                return Failure(ResultIpBlocked, "", true);
            }

            var sourceFailureCount = ipRateLimiter.RecentFailureCount(remoteEndPoint, packet.Id);

            if (AccountBlockGate.EvaluateThrottle(sourceFailureCount, account?.FailedLoginCount ?? 0,
                    account?.LockoutUntilUtc, DateTime.UtcNow) is AccountBlockOutcome.AutoLockedOut)
            {
                logger.LogWarning(
                    "Login rejected before credential verification: IP {RemoteIp} is over its failed-attempt budget for login {Id}",
                    remoteEndPoint, packet.Id);

                if (account is not null && ipRateLimiter.TryClaimThrottleReport(remoteEndPoint, packet.Id))
                    await LogThrottleRejectionAsync(account.AccountId, sourceFailureCount, remoteIp,
                        cancellationToken);

                return Failure(ResultCustomMessage, AccountLockedOutMessage, true);
            }

            AuthenticationOutcome authentication;
            try
            {
                authentication = await AuthenticateConstantTimeAsync(account, packet.Id, packet.Password,
                    remoteEndPoint, remoteIp, cancellationToken);
            }
            catch (CaeriusNetSqlException ex)
            {
                logger.LogError(ex, "Login failed: credential verification errored for login {Id}", packet.Id);
                return Failure(ResultUnknownAccount, "", true);
            }

            switch (authentication)
            {
                case AuthenticationOutcome.UnknownAccount:
                    logger.LogWarning("Login failed: login {Id} rejected with result code {ResultCode}", packet.Id,
                        ResultUnknownAccount);
                    return Failure(ResultUnknownAccount, "", true);
                case AuthenticationOutcome.WrongPassword:
                    logger.LogWarning("Login failed: login {Id} rejected with result code {ResultCode}", packet.Id,
                        ResultWrongPassword);
                    return Failure(ResultWrongPassword, "", true);
                case AuthenticationOutcome.AdminBanned:
                    logger.LogWarning("Login failed: login {Id} rejected with result code {ResultCode}", packet.Id,
                        ResultBlocked);
                    return Failure(ResultBlocked, "", true);
            }

            var accountId = account!.AccountId;

            if (string.IsNullOrEmpty(packet.Adapter.AdapterName))
            {
                logger.LogWarning("Login rejected: empty adapter name/GUID (login {Id}, account {AccountId})",
                    packet.Id, accountId);
                return Failure(ResultCustomMessage, AdapterNameEmptyMessage, true);
            }

            var quotaGateApplies = macAddress.Length > 0 && account.AccountGrade < DeviceSpoofingGuard.GmGradeThreshold;

            if (quotaGateApplies)
            {
                int? configuredAccountLimit;
                try
                {
                    configuredAccountLimit = await macRestrictions.GetConfiguredAccountLimitAsync(macAddress,
                        packet.Adapter.AdapterName, cancellationToken);
                }
                catch (CaeriusNetSqlException ex)
                {
                    logger.LogWarning(ex,
                        "Login: configured account-limit lookup failed for MAC {MacAddress} (account {AccountId}); treating as unconfigured, matching legacy's fail-open default",
                        macAddress, accountId);
                    configuredAccountLimit = null;
                }

                int concurrentDeviceSessionCount;
                if (configuredAccountLimit is null)
                    try
                    {
                        concurrentDeviceSessionCount = await accountSessions.GetConcurrentDeviceSessionCountAsync(
                            accountId, packet.Adapter.AdapterName, packet.Adapter.IPAddress, remoteIp ?? "",
                            cancellationToken);
                    }
                    catch (CaeriusNetSqlException ex)
                    {
                        logger.LogWarning(ex,
                            "Login: concurrent device-session-count lookup failed for account {AccountId}; falling back to the legacy dead-code baseline",
                            accountId);
                        concurrentDeviceSessionCount = PerAdapterLoginCapGate.DeadCodeLiveCountBaseline;
                    }
                else
                    concurrentDeviceSessionCount = 0;

                switch (PerAdapterLoginCapGate.Evaluate(account.AccountGrade, macAddress, configuredAccountLimit,
                            concurrentDeviceSessionCount))
                {
                    case PerAdapterLoginCapOutcome.OutrightBanned:
                        logger.LogWarning(
                            "Login rejected: MAC {MacAddress} is banned (login {Id}, account {AccountId})",
                            macAddress, packet.Id, accountId);
                        return Failure(ResultCustomMessage, MacBannedMessage, true);
                    case PerAdapterLoginCapOutcome.ConnectionLimitExceeded:
                        logger.LogWarning(
                            "Login rejected: per-adapter connection limit exceeded (login {Id}, account {AccountId})",
                            packet.Id, accountId);
                        return Failure(ResultCustomMessage, ConnectionLimitExceededMessage, true);
                }
            }

            try
            {
                await accountSessions.RecordDeviceSignatureAsync(accountId,
                    quotaGateApplies ? packet.Adapter.AdapterName : DeviceSpoofingGuard.PlaceholderAdapterGuid,
                    quotaGateApplies ? packet.Adapter.IPAddress : DeviceSpoofingGuard.PlaceholderRemoteIp,
                    quotaGateApplies ? remoteIp ?? "" : DeviceSpoofingGuard.PlaceholderRemoteIp, cancellationToken);
            }
            catch (CaeriusNetSqlException ex)
            {
                logger.LogWarning(ex,
                    "Login: device signature update failed for account {AccountId}; the per-adapter cap will not see this session",
                    accountId);
            }

            if (DeviceSpoofingGuard.IsSpoofedDeviceTuple(account.AccountGrade, macAddress, packet.Adapter.AdapterName,
                    remoteIp))
            {
                logger.LogWarning("Login rejected: spoofed device tuple detected for account {AccountId}", accountId);
                return Failure(ResultCustomMessage, InvalidDevicesMessage, true);
            }

            if (options.Value.OnlyAdminCanLogin && account.AccountGrade < DeviceSpoofingGuard.GmGradeThreshold)
            {
                logger.LogWarning(
                    "Login rejected: only-admin lockdown is active and account {AccountId} is not GM-tier", accountId);
                return Failure(ResultCustomMessage, OnlyAdminMessage, true);
            }

            AccountPinDto? storedPin;
            try
            {
                storedPin = await pins.GetAsync(accountId, cancellationToken);
            }
            catch (CaeriusNetSqlException ex)
            {
                logger.LogWarning(ex,
                    "Login: stored-PIN lookup failed for account {AccountId}; continuing with no PIN mask hint (not legacy-verified, see fenrir-security-hardening-engineer contract)",
                    accountId);
                storedPin = null;
            }

            var requirePin = options.Value.RequireSecondPassword;
            var pinMask = storedPin is null ? "" : LoginTrain.ExistingPinMask;

            CharacterAccountRosterBundle roster;
            ImmutableArray<AvatarRosterEntry> rosterEntries;
            try
            {
                roster = await characters.GetAccountRosterAsync(accountId, cancellationToken);
                rosterEntries = await ResolveRosterEntriesAsync(roster, cancellationToken);
            }
            catch (CaeriusNetSqlException ex)
            {
                logger.LogError(ex, "Login failed: avatar roster load errored for account {AccountId}", accountId);
                return Failure(ResultAvatarLoadFailed, "", true);
            }

            var newToken = Guid.NewGuid();
            AccountSessionClaimDto claim;
            try
            {
                claim = await accountSessions.ClaimOrSignalKickAsync(accountId, newToken, cancellationToken);
            }
            catch (CaeriusNetSqlException ex)
            {
                logger.LogError(ex,
                    "Login failed: account-session claim errored for account {AccountId} after authentication succeeded",
                    accountId);
                return Failure(ResultSessionRegistrationFailed, "", true);
            }

            switch ((AccountSessionClaimOutcome)claim.Outcome)
            {
                case AccountSessionClaimOutcome.ConflictLogin:
                    if (registry.TryGetByAccount(accountId, out var existingLocal))
                    {
                        logger.LogWarning(
                            "Login conflict: evicting stale local session for account {AccountId} (ConflictLogin)",
                            accountId);
                        existingLocal!.Abort(DisconnectReason.Evicted);
                        return LoginResult.SilentDropResult;
                    }

                    logger.LogWarning(
                        "Login rejected: account {AccountId} already claimed by another Login session (ConflictLogin, no local socket)",
                        accountId);
                    return Failure(ResultAlreadyConnected, "", true);
                case AccountSessionClaimOutcome.ConflictGameKicked:
                case AccountSessionClaimOutcome.ConflictTearingDown:
                    logger.LogWarning(
                        "Login rejected: account {AccountId} has a live Game session ({Outcome}); retry once it clears",
                        accountId, (AccountSessionClaimOutcome)claim.Outcome);
                    return Failure(ResultAlreadyConnected, "", true);
                case AccountSessionClaimOutcome.ReclaimedDeadShard:
                    logger.LogWarning(
                        "Login reclaimed account {AccountId}: its previous shard {ShardId} had a stale/missing heartbeat and was treated as dead rather than waiting for the reap sweep",
                        accountId, claim.PreviousShardId);
                    break;
            }

            await retiredItems.PurgeAsync(roster.Items, cancellationToken);

            if (registry.AssociateAccount(sessionId, accountId) is { } supersededLocal)
            {
                logger.LogWarning(
                    "Login: evicting a superseded local session for account {AccountId} that the DB claim did not report",
                    accountId);
                supersededLocal.Abort(DisconnectReason.Evicted);
            }

            try
            {
                await eventLog.LogAsync(LoginSucceededEventCode, EventLogCategory.Session, accountId, null, null, null,
                    null, null, null, null, null, 1, remoteIp is null ? null : $"RemoteIp={remoteIp}",
                    cancellationToken);
            }
            catch (CaeriusNetSqlException ex)
            {
                logger.LogWarning(ex, "Login: succeeded-login event-log write failed for account {AccountId}",
                    accountId);
            }

            logger.LogInformation(
                "Login succeeded: account {AccountId} authenticated, {CharacterCount} character(s), PIN required {RequirePin}",
                accountId, roster.Characters.Count, requirePin);

            reservationConsumed = true;
            return new LoginResult(LoginOutcome.Success, ResultSuccess, "", false, accountId, requirePin, pinMask,
                rosterEntries, newToken, account.AccountGrade);
        }
        finally
        {
            if (!reservationConsumed)
                capacity.ReleaseReservedSlot();
        }
    }

    private async ValueTask<ImmutableArray<AvatarRosterEntry>> ResolveRosterEntriesAsync(
        CharacterAccountRosterBundle roster, CancellationToken ct)
    {
        if (roster.Characters.Count == 0)
            return [];

        var itemsByCharacterId = roster.Items
            .GroupBy(i => i.CharacterId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CharacterRosterItemDto>)g.ToArray());

        var petBagByCharacterId = roster.PetBagSlots is null
            ? new Dictionary<int, IReadOnlyList<CharacterRosterPetBagSlotDto>>()
            : roster.PetBagSlots
                .GroupBy(p => p.CharacterId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<CharacterRosterPetBagSlotDto>)g.ToArray());

        var costumeByCharacterId = roster.CostumeSlots is null
            ? new Dictionary<int, IReadOnlyList<CharacterRosterCostumeSlotDto>>()
            : roster.CostumeSlots
                .GroupBy(c => c.CharacterId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<CharacterRosterCostumeSlotDto>)g.ToArray());

        var guildTasks = roster.Characters.Select(c => guilds.GetByCharacterAsync(c.CharacterId, ct).AsTask());
        var friendTasks = roster.Characters.Select(c => friends.GetByCharacterAsync(c.CharacterId, ct).AsTask());
        var mentorTasks = roster.Characters.Select(c => mentor.GetForCharacterAsync(c.CharacterId, ct).AsTask());

        var guildMemberships = await Task.WhenAll(guildTasks);
        var friendRows = await Task.WhenAll(friendTasks);
        var mentorBonds = await Task.WhenAll(mentorTasks);

        var entries = ImmutableArray.CreateBuilder<AvatarRosterEntry>(roster.Characters.Count);
        for (var i = 0; i < roster.Characters.Count; i++)
        {
            var character = roster.Characters[i];
            var friendNameBySlot = friendRows[i].ToDictionary(f => f.Slot, f => f.FriendName);
            var mentorBond = mentorBonds[i];

            entries.Add(new AvatarRosterEntry(
                character,
                itemsByCharacterId.GetValueOrDefault(character.CharacterId, []),
                guildMemberships[i]?.GuildName ?? "",
                friendNameBySlot,
                mentorBond?.TeacherName ?? "",
                mentorBond?.StudentName ?? "",
                petBagByCharacterId.GetValueOrDefault(character.CharacterId, []),
                costumeByCharacterId.GetValueOrDefault(character.CharacterId, [])));
        }

        return entries.ToImmutable();
    }

    private async ValueTask<bool> IsGmLoginIpAllowedAsync(string remoteIp, CancellationToken ct)
    {
        try
        {
            return await gmAllowlist.IsAllowedAsync(remoteIp, ct);
        }
        catch (CaeriusNetSqlException ex)
        {
            logger.LogError(ex,
                "Login: GM allowlist lookup errored for IP {RemoteIp}; treating the IP as not allowlisted", remoteIp);
            return false;
        }
    }

    private async ValueTask<AuthenticationOutcome> AuthenticateConstantTimeAsync(AuthenticateAccountDto? account,
        string loginName, string password, IPEndPoint? remoteEndPoint, string? remoteIp, CancellationToken ct)
    {
        if (account is null)
        {
            _ = PasswordHasher.Verify(password, DummyCredential.Hash, DummyCredential.Salt);
            ipRateLimiter.RecordFailure(remoteEndPoint, loginName);
            return AuthenticationOutcome.UnknownAccount;
        }

        var passwordOk = PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt);

        if (!passwordOk)
        {
            ipRateLimiter.RecordFailure(remoteEndPoint, loginName);
            await accounts.RecordLoginAttemptAsync(account.AccountId, false, ct);
            var windowOpen = account.LockoutUntilUtc > DateTime.UtcNow;
            var failureCount = windowOpen ? (byte)Math.Min(account.FailedLoginCount + 1, byte.MaxValue) : (byte)1;
            await eventLog.LogAsync(AccountSecurityEventCodes.LoginPasswordMismatch, EventLogCategory.AccountSecurity,
                account.AccountId, null, null, null, null, null, null, null, null, failureCount,
                remoteIp is null ? null : $"RemoteIp={remoteIp}", ct);
            return AuthenticationOutcome.WrongPassword;
        }

        ipRateLimiter.ClearFailures(remoteEndPoint, loginName);

        var loggedBan = await bans.IsActiveForAccountAsync(account.AccountId, ct);

        if (AccountBlockGate.EvaluateAdminBan(account.IsBanned, loggedBan) is AccountBlockOutcome.AdminBanned)
            return AuthenticationOutcome.AdminBanned;

        await accounts.RecordLoginAttemptAsync(account.AccountId, true, ct);
        return AuthenticationOutcome.Success;
    }

    private async ValueTask LogThrottleRejectionAsync(int accountId, int sourceFailureCount, string? remoteIp,
        CancellationToken ct)
    {
        try
        {
            await eventLog.LogAsync(AccountSecurityEventCodes.LoginPasswordAttemptRejectedLocked,
                EventLogCategory.AccountSecurity, accountId, null, null, null, null, null, null, null, null,
                (byte)Math.Min(sourceFailureCount, byte.MaxValue),
                remoteIp is null ? null : $"RemoteIp={remoteIp}", ct);
        }
        catch (CaeriusNetSqlException ex)
        {
            logger.LogWarning(ex, "Login: throttle-rejection event-log write failed for account {AccountId}",
                accountId);
        }
    }

    private static LoginResult Failure(int resultCode, string resultString, bool reArmVersionOk)
    {
        return new LoginResult(LoginOutcome.Failure, resultCode, resultString, reArmVersionOk, 0, false, "", []);
    }

    private enum AuthenticationOutcome
    {
        Success,
        UnknownAccount,
        WrongPassword,
        AdminBanned
    }
}
