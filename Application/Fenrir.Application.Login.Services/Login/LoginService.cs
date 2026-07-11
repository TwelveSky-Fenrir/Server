using System.Collections.Immutable;
using System.Net;
using Fenrir.Application.Login.Abstractions.Login;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Domain.RateLimiting;
using Fenrir.Application.Login.Domain.Security;
using Fenrir.Data.Security;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Microsoft.Data.SqlClient;
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

    private const int ResultCustomMessage = 10000;

    private const int ResultSuccess = 0;

    private const string MacBannedMessage = "Your PC has been banned.";
    private const string InvalidDevicesMessage = "Invalid devices!";
    private const string OnlyAdminMessage = "Only Admin can login";

    private const string AdapterNameEmptyMessage = "IP address not specified. Please update to the latest client.";

    private const short LoginSucceededEventCode = 1;

    private static readonly (byte[] Hash, byte[] Salt) DummyCredential =
        PasswordHasher.Hash("dummy-unused-reference-password");

    public async ValueTask<LoginResult> LoginAsync(long sessionId, IPEndPoint? remoteEndPoint, LoginRequest packet,
        CancellationToken cancellationToken)
    {
        if (!ipRateLimiter.TryConsume(remoteEndPoint))
        {
            logger.LogWarning("Login rejected: IP {RemoteIp} exceeded its rate-limit budget", remoteEndPoint);
            return LoginResult.RateLimitedResult;
        }

        switch (LoginCapacityGate.Evaluate(capacity.MaxPlayers, capacity.CurrentPlayers))
        {
            case LoginCapacityOutcome.Maintenance:
                logger.LogWarning("Login rejected: server is under maintenance (login {Id})", packet.Id);
                return Failure(ResultMaintenance, "", false);
            case LoginCapacityOutcome.ServerFull:
                logger.LogWarning("Login rejected: server is full (login {Id})", packet.Id);
                return Failure(ResultServerFull, "", false);
        }

        if (!await firewall.IsAllowedAsync(remoteEndPoint, cancellationToken))
        {
            logger.LogWarning("Login rejected: IP {RemoteIp} is blocked by the application firewall", remoteEndPoint);
            return Failure(ResultIpBlocked, "", false);
        }

        if (packet.Version != options.Value.ExpectedClientVersion)
        {
            logger.LogWarning("Login rejected: client version mismatch (login {Id}, version {Version})", packet.Id,
                packet.Version);
            return Failure(ResultVersionMismatch, "", false);
        }

        var macAddress =
            MacAddressFormatter.Format(packet.Adapter.PhysicalAddress, packet.Adapter.PhysicalAddressLength);
        if (macAddress.Length > 0 &&
            await macRestrictions.IsBannedAsync(macAddress, packet.Adapter.AdapterName, cancellationToken))
        {
            logger.LogWarning("Login rejected: MAC {MacAddress} is banned (login {Id})", macAddress, packet.Id);
            return Failure(ResultCustomMessage, MacBannedMessage, false);
        }

        var account = await accounts.AuthenticateAsync(packet.Id, cancellationToken);
        var remoteIp = remoteEndPoint?.Address.ToString();

        if (account is not null && account.AccountGrade >= DeviceSpoofingGuard.GmGradeThreshold &&
            remoteIp is not null && !await gmAllowlist.IsAllowedAsync(remoteIp, cancellationToken))
        {
            _ = PasswordHasher.Verify(packet.Password, account.PasswordHash, account.PasswordSalt);
            logger.LogWarning(
                "Login rejected: GM-tier account {AccountId} attempted login from non-allowlisted IP {RemoteIp}",
                account.AccountId, remoteIp);
            return Failure(ResultIpBlocked, "", true);
        }

        var result = await AuthenticateConstantTimeAsync(account, packet.Password, cancellationToken);

        if (result != ResultSuccess)
        {
            logger.LogWarning("Login failed: login {Id} rejected with result code {ResultCode}", packet.Id, result);
            return Failure(result, "", true);
        }

        var accountId = account!.AccountId;

        if (options.Value.OnlyAdminCanLogin && account.AccountGrade < DeviceSpoofingGuard.GmGradeThreshold)
        {
            logger.LogWarning(
                "Login rejected: only-admin lockdown is active and account {AccountId} is not GM-tier", accountId);
            return Failure(ResultCustomMessage, OnlyAdminMessage, true);
        }

        if (string.IsNullOrEmpty(packet.Adapter.AdapterName))
        {
            logger.LogWarning("Login rejected: empty adapter name/GUID (login {Id}, account {AccountId})",
                packet.Id, accountId);
            return Failure(ResultCustomMessage, AdapterNameEmptyMessage, true);
        }

        if (DeviceSpoofingGuard.IsSpoofedDeviceTuple(account.AccountGrade, macAddress, packet.Adapter.AdapterName,
                remoteIp))
        {
            logger.LogWarning("Login rejected: spoofed device tuple detected for account {AccountId}", accountId);
            return Failure(ResultCustomMessage, InvalidDevicesMessage, true);
        }

        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        var requirePin = options.Value.RequireSecondPassword;
        var pinMask = storedPin is null ? "" : LoginTrain.ExistingPinMask;

        var newToken = Guid.NewGuid();
        AccountSessionClaimDto claim;
        try
        {
            claim = await accountSessions.ClaimOrSignalKickAsync(accountId, newToken, cancellationToken);
        }
        catch (SqlException ex)
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

        registry.AssociateAccount(sessionId, accountId);

        var roster = await characters.GetAccountRosterAsync(accountId, cancellationToken);
        var rosterEntries = await ResolveRosterEntriesAsync(roster, cancellationToken);

        await eventLog.LogAsync(LoginSucceededEventCode, EventLogCategory.Session, accountId, null, null, null,
            null, null, null, null, null, 1, remoteIp is null ? null : $"RemoteIp={remoteIp}", cancellationToken);

        logger.LogInformation(
            "Login succeeded: account {AccountId} authenticated, {CharacterCount} character(s), PIN required {RequirePin}",
            accountId, roster.Characters.Count, requirePin);

        return new LoginResult(LoginOutcome.Success, ResultSuccess, "", false, accountId, requirePin, pinMask,
            rosterEntries, newToken, account.AccountGrade);
    }

    private async ValueTask<ImmutableArray<AvatarRosterEntry>> ResolveRosterEntriesAsync(
        CharacterAccountRosterBundle roster, CancellationToken ct)
    {
        if (roster.Characters.Count == 0)
            return [];

        var itemsByCharacterId = roster.Items
            .GroupBy(i => i.CharacterId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CharacterRosterItemDto>)g.ToArray());

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
                mentorBond?.StudentName ?? ""));
        }

        return entries.ToImmutable();
    }

    private async ValueTask<int> AuthenticateConstantTimeAsync(AuthenticateAccountDto? account, string password,
        CancellationToken ct)
    {
        if (account is null)
        {
            _ = PasswordHasher.Verify(password, DummyCredential.Hash, DummyCredential.Salt);
            return ResultUnknownAccount;
        }

        var loggedBan = await bans.IsActiveForAccountAsync(account.AccountId, ct);
        if (account.IsBanned || loggedBan || account.LockoutUntilUtc > DateTime.UtcNow)
        {
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
