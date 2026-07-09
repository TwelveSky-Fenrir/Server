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

/// <summary>
///     op11 CL_LOGIN_SEND business logic -- IP rate limit (Fenrir-only, no legacy analog), then maintenance
///     lockdown, then server-full quota, then the application firewall, then version, then MAC restriction,
///     then the account lookup, then the GM-IP allowlist gate, then auth (password/ban), then the "Only Admin
///     can login" lockdown, then the empty-adapter-name gate, then the device anti-spoofing gate, run in that
///     order so an under-maintenance/over-capacity/over-budget/blocked/incompatible/banned-PC attempt never
///     reaches Argon2id/SQL account lookup, a GM-tier account from a non-allowlisted IP never even reaches the
///     password comparison (matching legacy, see the GM-IP gate's own remarks below), and an admin-lockdown/
///     missing-adapter/spoofed-device rejection never reaches PIN/session-claim, unnecessarily.
/// </summary>
/// <remarks>
///     Réf. C++ (maintenance/quota) : Server/ts25login/S04_MyWork02.cpp:149-154 (maintenance, tResult=1) et
///     :155-160 (quota serveur plein, tResult=3) -- les deux premiers contrôles du handler legacy, avant même le
///     contrôle de version (:161-166) ; voir <see cref="LoginCapacityGate" />/<see cref="LoginCapacityState" />
///     pour le détail et Fenrir.Application.Login.Hosting.ServerQuotaRefreshHost pour le rafraîchissement
///     continu (~1s) de cet état, jamais relu en ligne dans ce chemin de requête. Le rate limiter IP ci-dessus
///     reste volontairement en premier malgré cet ordre legacy : c'est un ajout Fenrir sans équivalent legacy,
///     protégeant contre le flood indépendamment de l'état maintenance/quota.
///     Réf. C++ (MAC-restriction gate order) : legacy's mac_limit check (Server/ts25login/S08_MyDB.cpp:441-448)
///     only runs after both the password check (:363-370) and the block/ban check (:372-376) have already
///     passed inside MyDB::Login, whereas Fenrir checks it before the account lookup entirely -- a deliberate
///     divergence, not an oversight; see the MAC-restriction check's own remarks below for why.
///     Réf. C++ (GM-IP allowlist gate order) : Server/ts25login/S08_MyDB.cpp:339-356 (GM-IP check) precedes
///     :357-370 (CheckValidUserIdx/password) inside MyDB::Login -- see that gate's own remarks below.
///     Réf. C++ (empty-adapter-name gate) : Server/ts25login/S08_MyDB.cpp:421-425 -- see that gate's own
///     remarks below.
///     Réf. C++ (device anti-spoofing gate) : see <see cref="DeviceSpoofingGuard" />'s own remarks for the full
///     citation range (Server/ts25login/S08_MyDB.cpp:497-507 and siblings).
/// </remarks>
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
    // Legacy tResult codes actually producible by Fenrir's flows (S04_MyWork02.cpp W_LOGIN_SEND / S08_MyDB Login):
    private const int ResultMaintenance = 1; // MyGame::mMaxPlayerNum == 0 (S04_MyWork02.cpp:149-154)
    // MyDB::CheckGMIP/CheckBanIP (S04_MyWork02.cpp:195-215), and -- the one Fenrir's GM-IP-allowlist gate
    // below actually mirrors -- the GM-IP gate inside MyDB::Login itself (S08_MyDB.cpp:339-356), which runs
    // strictly before the password comparison.
    private const int ResultIpBlocked = 2;
    private const int ResultServerFull = 3; // MyGame::mPresentPlayerNum >= mMaxPlayerNum (S04_MyWork02.cpp:155-160)
    private const int ResultVersionMismatch = 4; // tVersion != mServerVersion

    /// <summary>
    ///     Shared code for ts25playuser's RegisterUserForLogin_01 capacity-full (<c>_failed2</c>,
    ///     Server/ts25playuser/S07_MyGame01.cpp:915-918, its own fixed 2000-slot table full -- practically
    ///     unreachable under the shipped MaxUser=1000 login quota, but still live code) and
    ///     DB-persistence-failure (<c>_failed3</c>, S07_MyGame01.cpp:920-923) exits, plus an unrelated
    ///     catch-all default case (Server/ts25login/S04_MyWork02.cpp:297-300) -- all three collapse onto this
    ///     one tResult (S04_MyWork02.cpp:285-292); the client cannot distinguish any of them. Fenrir has no
    ///     fixed-size registration table to overflow, so only the persistence-failure analog is reachable
    ///     here: see the catch around <see cref="IAccountSessionRepository.ClaimOrSignalKickAsync" /> below.
    /// </summary>
    private const int ResultSessionRegistrationFailed = 5;

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

    // mDB.Login: shared "free-text application message" result code -- also carries the "Protect Spoofed"
    // gate's "Invalid devices!" message (Server/ts25login/S08_MyDB.cpp:497-507) alongside the unrelated
    // macinfo mac_limit < 1 case below; the legacy client has no finer-grained handling to distinguish them.
    private const int ResultCustomMessage = 10000;

    private const int ResultSuccess = 0;

    private const string MacBannedMessage = "Your PC has been banned.";
    private const string InvalidDevicesMessage = "Invalid devices!";
    private const string OnlyAdminMessage = "Only Admin can login";

    // Legacy unconditional adapter-name/GUID presence gate (Server/ts25login/S08_MyDB.cpp:421-425).
    private const string AdapterNameEmptyMessage = "IP address not specified. Please update to the latest client.";

    /// <summary>
    ///     game.EventLog.EventCode for a successful authentication (Category=Session) -- an app-owned numbering
    ///     scheme with no central catalog yet (see game.EventLog.sql's own "app-owned numbering scheme" comment).
    ///     Scoped within EventLogCategory.Session alongside the other three session-lifecycle events wired so
    ///     far: Fenrir.Application.Login.Hosting.LoginConnectionHost's LoginSessionEndedEventCode (2),
    ///     Fenrir.Application.Game.Services.ZoneLifecycle.ZoneHandshakeService's ZoneTransferAcceptedEventCode
    ///     (3), and Fenrir.Application.Game.Hosting.GameConnectionHost's LogoutEventCode (4) -- a future central
    ///     event-code registry should supersede these four rather than silently reusing one of their numeric
    ///     values for something unrelated.
    /// </summary>
    private const short LoginSucceededEventCode = 1;

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
        {
            logger.LogWarning("Login rejected: IP {RemoteIp} exceeded its rate-limit budget", remoteEndPoint);
            return LoginResult.RateLimitedResult;
        }

        // Maintenance lockdown / server-full quota: the two earliest gates in the legacy handler, evaluated
        // before every other check below (including the firewall/version/MAC/auth gates none of which the
        // legacy source ran this early). Both read only continuously-refreshed server-wide state -- no packet
        // field, no database round trip on this request path itself (ServerQuotaRefreshHost owns that).
        switch (LoginCapacityGate.Evaluate(capacity.MaxPlayers, capacity.CurrentPlayers))
        {
            case LoginCapacityOutcome.Maintenance:
                logger.LogWarning("Login rejected: server is under maintenance (login {Id})", packet.Id);
                return Failure(ResultMaintenance, "", false);
            case LoginCapacityOutcome.ServerFull:
                logger.LogWarning("Login rejected: server is full (login {Id})", packet.Id);
                return Failure(ResultServerFull, "", false);
        }

        // Checked ahead of Argon2id/SQL (unlike legacy, which only ran this after a successful mDB.Login): an
        // already-known-bad IP shouldn't get a free password-guessing attempt against any account.
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

        // MAC-restriction ("Your PC has been banned") gate: deliberately checked here, ahead of the account
        // lookup and password verification entirely. Legacy's mac_limit check runs much later -- only after
        // both the password check (Server/ts25login/S08_MyDB.cpp:363-370) and the block/ban check (:372-376)
        // have already passed inside MyDB::Login (mac_limit itself is at :441-448) -- but Fenrir moving it
        // ahead of auth is an intentional hardening, not an unflagged accident (contrast with the
        // ApplicationFirewall reordering documented a few lines above, which this follows the same posture
        // as): it rejects a known-bad device signal before spending any Argon2id/DB work on it, and it closes
        // a legacy password-correctness oracle -- because legacy's mac_limit check only runs after a correct
        // password already matched, a banned-MAC attacker who gets back "Your PC has been banned" (rather
        // than "wrong password") thereby learns their password guess was actually correct, even though the
        // login itself is still refused. Checking the MAC ban first, before any password comparison ever
        // runs, means the same rejection message comes back regardless of whether the supplied password is
        // right or wrong, so that oracle doesn't exist in Fenrir.
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

        // "# GM Enable Login IP #" gate: legacy evaluates this strictly before the password comparison,
        // inside MyDB::Login itself (Server/ts25login/S08_MyDB.cpp:339-356 precedes the password check at
        // :357-370) -- once the account row is located, a GM-tier account (uUserSort > 0) connecting from a
        // non-allowlisted IP is refused immediately, before the password is ever compared, so a
        // wrong-password attempt against such an account still returns tResult=2 ("IP blocked"), never
        // tResult=7 ("wrong password"). Runs here, right after the account lookup and ahead of
        // AuthenticateConstantTimeAsync, to match that ordering; the discarded Argon2id verify below keeps
        // this branch's wall-clock cost the same as a real password check, preserving
        // AuthenticateConstantTimeAsync's timing-attack defense even though the reject can now happen ahead
        // of it in the pipeline. Fails open when no remote address is known (unit tests / non-TCP transport),
        // matching ApplicationFirewall.IsAllowedAsync's own documented fail-open rationale for the identical
        // condition.
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
            // Re-arms VersionOk so the client can retry on this same connection without a reconnect.
            return Failure(result, "", true);
        }

        var accountId = account!.AccountId;

        // Legacy "Only Admin can login" operator lockdown (Server/ts25login/S04_MyWork02.cpp:202-208): when
        // enabled, only GM-tier accounts may proceed past this point. Disabled by default (see
        // LoginServerOptions.OnlyAdminCanLogin's own remarks).
        if (options.Value.OnlyAdminCanLogin && account.AccountGrade < DeviceSpoofingGuard.GmGradeThreshold)
        {
            logger.LogWarning(
                "Login rejected: only-admin lockdown is active and account {AccountId} is not GM-tier", accountId);
            return Failure(ResultCustomMessage, OnlyAdminMessage, true);
        }

        // Legacy unconditional adapter-name/GUID presence gate (Server/ts25login/S08_MyDB.cpp:421-425):
        // "if (IsEmptyString(adapter->AdapterName)) { resString = "IP address not specified. Please update to
        // the latest client."; return 10000; }" -- runs inside MyDB::Login after the password/ban checks have
        // already passed (:363-370, :372-376), unconditional on uUserSort (applies to GM and non-GM accounts
        // alike), ahead of the mac_limit gate (:441-448) and the "Protect Spoofed" gate (:497-507). No
        // equivalent existed anywhere in LoginService/DeviceSpoofingGuard before this.
        if (string.IsNullOrEmpty(packet.Adapter.AdapterName))
        {
            logger.LogWarning("Login rejected: empty adapter name/GUID (login {Id}, account {AccountId})",
                packet.Id, accountId);
            return Failure(ResultCustomMessage, AdapterNameEmptyMessage, true);
        }

        // "Protect Spoofed" anti-spoofing gate: runs after the account row is located, the password has
        // matched, and the block/ban check has already passed (see AuthenticateConstantTimeAsync above) --
        // the same point the legacy routine evaluates it at. Re-arms VersionOk like every other post-auth-
        // success failure (ConflictLogin/ConflictGameKicked/ConflictTearingDown below do the same).
        if (DeviceSpoofingGuard.IsSpoofedDeviceTuple(account.AccountGrade, macAddress, packet.Adapter.AdapterName,
                remoteIp))
        {
            logger.LogWarning("Login rejected: spoofed device tuple detected for account {AccountId}", accountId);
            return Failure(ResultCustomMessage, InvalidDevicesMessage, true);
        }

        // "****" (PIN exists) vs "" (must create) is what the legacy client keys the op13/op15 choice on.
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        var requirePin = options.Value.RequireSecondPassword;
        var pinMask = storedPin is null ? "" : LoginTrain.ExistingPinMask;

        // Cross-process duplicate-login authority (runtime.AccountSessions) -- see ResultAlreadyConnected's
        // remarks for the ServerDocs citation behind tResult=8. Every successful credential check claims exactly
        // once; the claim's own atomic side effect (clear a stale row / flag a live Game row for kick / refuse a
        // tearing-down row) happens server-side in usp_AccountSession_ClaimOrSignalKick.
        var newToken = Guid.NewGuid();
        AccountSessionClaimDto claim;
        try
        {
            claim = await accountSessions.ClaimOrSignalKickAsync(accountId, newToken, cancellationToken);
        }
        catch (SqlException ex)
        {
            // Closest present-day analog to legacy's PlayUser _failed2/_failed3 exits (see
            // ResultSessionRegistrationFailed's remarks): the claim write exhausted its own bounded retry
            // budget (AccountSessionRepository.MaxClaimAttempts) or hit an unrelated SqlException (e.g. a
            // timeout), after authentication already succeeded. Legacy never leaves the client without a
            // reply on this path (tResult=5, S04_MyWork02.cpp:285-292) -- reply explicitly here instead of
            // letting the exception propagate uncaught to SessionLoop's generic dispatch catch, which would
            // tear the connection down without ever sending a wire-level failure code first.
            logger.LogError(ex,
                "Login failed: account-session claim errored for account {AccountId} after authentication succeeded",
                accountId);
            return Failure(ResultSessionRegistrationFailed, "", true);
        }

        switch ((AccountSessionClaimOutcome)claim.Outcome)
        {
            case AccountSessionClaimOutcome.ConflictLogin:
                // "Déjà connecté sur ts25login lui-même": kick the stale local session directly (matches the
                // legacy kick-then-tResult=8 shape -- the new attempt is refused too, not handed the account).
                if (registry.TryGetByAccount(accountId, out var existingLocal))
                {
                    logger.LogWarning(
                        "Login conflict: evicting stale local session for account {AccountId} (ConflictLogin)",
                        accountId);
                    existingLocal!.Abort(DisconnectReason.Evicted);
                    return LoginResult.SilentDropResult;
                }

                // Row claimed to be a live Login session but no matching local socket -- treat like any other refusal.
                logger.LogWarning(
                    "Login rejected: account {AccountId} already claimed by another Login session (ConflictLogin, no local socket)",
                    accountId);
                return Failure(ResultAlreadyConnected, "", true);
            case AccountSessionClaimOutcome.ConflictGameKicked:
            case AccountSessionClaimOutcome.ConflictTearingDown:
                // A live Game-side session was just flagged for kick (or is mid-teardown): refuse this attempt,
                // the account isn't free yet -- the player retries once the kick/teardown completes.
                logger.LogWarning(
                    "Login rejected: account {AccountId} has a live Game session ({Outcome}); retry once it clears",
                    accountId, (AccountSessionClaimOutcome)claim.Outcome);
                return Failure(ResultAlreadyConnected, "", true);
            case AccountSessionClaimOutcome.ReclaimedDeadShard:
                // usp_AccountSession_ClaimOrSignalKick found the account's previous Game-side row pointing at a
                // shard whose runtime.GameServerDirectory heartbeat had gone stale (or vanished outright) and
                // fast-cleared + reclaimed it immediately, rather than leaving it for AccountSessionReapHost's
                // 6-minute sweep to eventually notice -- proceed exactly like Registered, just logged distinctly
                // for operational visibility (see Database/Migrations/023_account_session_dead_shard_fast_reclaim.sql).
                logger.LogWarning(
                    "Login reclaimed account {AccountId}: its previous shard {ShardId} had a stale/missing heartbeat and was treated as dead rather than waiting for the reap sweep",
                    accountId, claim.PreviousShardId);
                break;
        }

        // Evicts any previous login session for this account still held locally (legacy playuser-result-4 local
        // kick) -- belt-and-braces alongside the cross-process claim above, since AssociateAccount's own eviction
        // only ever sees sessions on this same process.
        registry.AssociateAccount(sessionId, accountId);

        var roster = await characters.GetAccountRosterAsync(accountId, cancellationToken);
        var rosterEntries = await ResolveRosterEntriesAsync(roster, cancellationToken);

        // Session-lifecycle audit row: this is the "Login" leg (see LoginSucceededEventCode's remarks for the
        // other three legs). No character is chosen yet at this point in the flow, so ActorCharacterId is null.
        await eventLog.LogAsync(LoginSucceededEventCode, EventLogCategory.Session, accountId, null, null, null,
            null, null, null, null, null, 1, remoteIp is null ? null : $"RemoteIp={remoteIp}", cancellationToken);

        logger.LogInformation(
            "Login succeeded: account {AccountId} authenticated, {CharacterCount} character(s), PIN required {RequirePin}",
            accountId, roster.Characters.Count, requirePin);

        return new LoginResult(LoginOutcome.Success, ResultSuccess, "", false, accountId, requirePin, pinMask,
            rosterEntries, newToken, account.AccountGrade);
    }

    /// <summary>
    ///     Turns the raw <see cref="CharacterAccountRosterBundle" /> read into one <see cref="AvatarRosterEntry" />
    ///     per occupied slot, fanning out the live guild/friend/mentor lookups per character -- same "bounded
    ///     fan-out costs nothing meaningful on this already multi-round-trip login path" posture the prior
    ///     guild-only resolution already established, extended to the two additional lookups
    ///     <c>usp_Character_GetAccountRoster</c>'s own header deliberately leaves to the caller (same
    ///     composition <c>EnterWorldService.HandleAsync</c> already uses for the single-character world-entry
    ///     path). See <c>LoginTrain.BuildAvatarSlots</c>' own remarks for the full legacy citation.
    /// </summary>
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
