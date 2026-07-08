using System.Net;
using System.Runtime.CompilerServices;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.RateLimiting;
using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.Login;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Security;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Handlers;

// op11 CL_LOGIN_SEND -- cluster C02: application firewall (IP block/GM-allowlist bypass), MAC-restriction
// ("banned PC"), and the admin.Bans ban-log check layered on top of the pre-existing account.IsBanned flag.
public class LoginHandlerTests
{
    private const int ClientVersion = 90354; // LoginServerOptions.ExpectedClientVersion default
    private static readonly IPEndPoint RemoteEndPoint = new(IPAddress.Parse("203.0.113.50"), 40000);

    // Realistic, non-placeholder default device tuple: a real client always has a non-zero-length MAC, so
    // the anti-spoofing gate (cluster C09, DeviceSpoofingGuard) never trips unless a test deliberately
    // asks for the zero-length-MAC or placeholder-literal edge cases it covers below.
    private static readonly byte[] DefaultPhysicalAddress = ParseMac("11-22-33-44-55-66");

    // Login-time maintenance lockdown / server-full quota (Server/ts25login/S04_MyWork02.cpp:149-160): the two
    // earliest gates in the handler, evaluated before the firewall/version/MAC/auth checks below.
    [Fact]
    public async Task HandleAsync_MaintenanceMode_SendsMaintenanceResult_AndNeverAuthenticates()
    {
        var accounts = FakeAccountRepository.WithNoAccount();
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(0);
        var handler = CreateHandler(out var session, out var pipe, accounts, capacity: capacity);

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(1, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Equal(0, accounts.AuthenticateCallCount);
    }

    [Fact]
    public async Task HandleAsync_MaintenanceMode_TakesPrecedenceOverAnUnrelatedBlockedIp()
    {
        // Proves ordering: maintenance is checked before the firewall, not just before version/MAC/auth.
        var accounts = FakeAccountRepository.WithNoAccount();
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(0);
        var handler = CreateHandler(out var session, out var pipe, accounts, true, capacity: capacity);

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(1, "someuser", 0, LoginTrain.FailurePinMask));
    }

    [Fact]
    public async Task HandleAsync_ServerFull_CurrentPlayersEqualsMax_SendsServerFullResult_AndNeverAuthenticates()
    {
        var accounts = FakeAccountRepository.WithNoAccount();
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(100);
        capacity.SetCurrentPlayers(100);
        var handler = CreateHandler(out var session, out var pipe, accounts, capacity: capacity);

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(3, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Equal(0, accounts.AuthenticateCallCount);
    }

    [Fact]
    public async Task HandleAsync_ServerFull_CurrentPlayersBelowMax_ProceedsPastTheCapacityGate()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(100);
        capacity.SetCurrentPlayers(99);
        var handler = CreateHandler(out var session, out var pipe, accounts, capacity: capacity);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
    }

    [Fact]
    public async Task HandleAsync_IpIsBlocked_SendsIpBlockedResult_AndNeverAuthenticates()
    {
        var accounts = FakeAccountRepository.WithNoAccount();
        var handler = CreateHandler(out var session, out var pipe, accounts,
            true);

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(2, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Equal(0, accounts.AuthenticateCallCount);
    }

    [Fact]
    public async Task HandleAsync_IpOnGmAllowlist_BypassesTheBlockedIpList_AndProceedsToAuthenticate()
    {
        var accounts = FakeAccountRepository.WithNoAccount();
        var handler = CreateHandler(out var session, out var pipe, accounts,
            true, gmAllowlisted: true);

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        Assert.Equal(1, accounts.AuthenticateCallCount);
        // ResultUnknownAccount (6) proves it reached authentication instead of being turned away as IP-blocked.
        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(6, "someuser", 0, LoginTrain.FailurePinMask));
    }

    [Fact]
    public async Task HandleAsync_MacAddressIsBanned_SendsCustomMessageResult_AndNeverAuthenticates()
    {
        const string bannedMac = "00-11-22-33-44-55";
        var accounts = FakeAccountRepository.WithNoAccount();
        var handler = CreateHandler(out var session, out var pipe, accounts,
            bannedMacAddresses: [bannedMac]);

        await handler.HandleAsync(ValidLoginRequest(physicalAddress: ParseMac(bannedMac)), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask, "Your PC has been banned."));
        Assert.Equal(0, accounts.AuthenticateCallCount);
    }

    [Fact]
    public async Task HandleAsync_AccountHasAnActiveBanLogEntry_SendsBlockedResult_EvenWhenIsBannedFlagIsFalse()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(1, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, accountBanned: true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(9, "someuser", 0, LoginTrain.FailurePinMask));
    }

    [Fact]
    public async Task HandleAsync_NoRestrictionsAndValidCredentials_Succeeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        // RequireSecondPassword defaults to true (secondLoginSort=1); no stored PIN yet (pinMask="").
        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.NotNull(session.AccountSessionToken);
        // GM-BLOCK precondition: a non-elevated account must leave the session at grade 0.
        Assert.Equal((short)0, session.AccountGrade);
    }

    // Cross-process duplicate-login kick/refusal: runtime.AccountSessions flags a live Login-side row for this
    // same account (ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:145 "case 4" -- kick the
    // stale local session directly). The new attempt is dropped too, silently, exactly like RateLimited.
    [Fact]
    public async Task
        HandleAsync_ConflictLogin_WithALiveLocalSession_EvictsTheOldSession_AndDropsTheNewAttemptSilently()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var registry = new SessionRegistry();
        var staleSession = new LoginClientSession(99, new FakeDuplexPipe());
        registry.Register(staleSession);
        registry.AssociateAccount(99, 7);

        var accountSessions = new FakeAccountSessionRepository
            { ClaimOutcome = AccountSessionClaimOutcome.ConflictLogin };
        var handler = CreateHandler(out var session, out var pipe, accounts, registry: registry,
            accountSessions: accountSessions);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Evicted, staleSession.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Null(session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_ConflictLogin_WithNoMatchingLocalSession_SendsAlreadyConnectedResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var accountSessions = new FakeAccountSessionRepository
            { ClaimOutcome = AccountSessionClaimOutcome.ConflictLogin };
        var handler = CreateHandler(out var session, out var pipe, accounts, accountSessions: accountSessions);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(8, "someuser", 0, LoginTrain.FailurePinMask));
    }

    [Theory]
    [InlineData(AccountSessionClaimOutcome.ConflictGameKicked)]
    [InlineData(AccountSessionClaimOutcome.ConflictTearingDown)]
    public async Task HandleAsync_AccountAlreadyClaimedElsewhere_SendsAlreadyConnectedResult(
        AccountSessionClaimOutcome outcome)
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var accountSessions = new FakeAccountSessionRepository { ClaimOutcome = outcome };
        var handler = CreateHandler(out var session, out var pipe, accounts, accountSessions: accountSessions);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(8, "someuser", 0, LoginTrain.FailurePinMask));
    }

    // ts25playuser's RegisterUserForLogin_01 capacity-full/DB-persistence-failure exits (_failed2/_failed3,
    // S07_MyGame01.cpp:915-923) collapse in ts25login onto tResult=5 (S04_MyWork02.cpp:285-292), always an
    // explicit reply, never a silent drop. The claim write exhausting its own bounded retry budget (or
    // hitting an unrelated SqlException) is the closest present-day analog -- this must translate into the
    // same explicit LC_LOGIN_RECV{Result=5} reply rather than propagating uncaught out of LoginService.
    [Fact]
    public async Task HandleAsync_AccountSessionClaimThrowsSqlException_SendsSessionRegistrationFailedResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var accountSessions = new FakeAccountSessionRepository { ClaimException = CreateFakeSqlException() };
        var handler = CreateHandler(out var session, out var pipe, accounts, accountSessions: accountSessions);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(5, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Null(session.AccountId);
        // ReArmVersionOk=true (like every other post-auth-success failure): the client can retry on this
        // same connection without a reconnect.
        Assert.Equal(LoginSessionState.VersionOk, session.State);
    }

    // "# GM Enable Login IP #" gate (Server/ts25login/S04_MyWork02.cpp:192-201): a GM-tier account whose
    // credentials matched must still come from an allow-listed IP, or the whole login is refused with the
    // same IP-blocked result code (2) a plain banned IP gets.
    [Fact]
    public async Task HandleAsync_GmAccount_NonAllowlistedIp_SendsIpBlockedResult_AndNeverCompletesLogin()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: false);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(2, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Null(session.AccountId);
        // ReArmVersionOk=true: this trips post-authentication, so the client can retry on this same
        // connection, unlike the pre-auth IpIsBlocked case above.
        Assert.Equal(LoginSessionState.VersionOk, session.State);
    }

    // Symmetric positive case: a GM-tier account from an allow-listed IP proceeds normally, proving the gate
    // is a real check (not an always-reject) and doesn't interfere with a legitimate GM login.
    [Fact]
    public async Task HandleAsync_GmAccount_AllowlistedIp_Succeeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.Equal((short)1, session.AccountGrade);
    }

    // A non-GM account is entirely unaffected by the GM-IP gate regardless of allowlist state -- the
    // condition is gated on AccountGrade, not evaluated unconditionally for every login.
    [Fact]
    public async Task HandleAsync_NonGmAccount_NonAllowlistedIp_StillSucceeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: false);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
    }

    // Legacy "Only Admin can login" operator lockdown (Server/ts25login/S04_MyWork02.cpp:202-208): when
    // enabled, a non-GM-tier account is refused with the fixed application message, even with fully valid
    // credentials and no other restriction in play.
    [Fact]
    public async Task HandleAsync_OnlyAdminLockdownEnabled_NonGmAccount_SendsCustomMessageResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, onlyAdminCanLogin: true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask, "Only Admin can login"));
        Assert.Null(session.AccountId);
    }

    // A GM-tier account is exempt from the lockdown -- the flag only turns away non-elevated accounts.
    [Fact]
    public async Task HandleAsync_OnlyAdminLockdownEnabled_GmAccount_Succeeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: true,
            onlyAdminCanLogin: true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
    }

    // "Protect Spoofed" anti-spoofing gate (Server/ts25login/S08_MyDB.cpp:497-507): a non-GM account whose
    // declared MAC is zero-length never gets its device values populated with real data at all, so the gate
    // always trips regardless of whatever GUID/IP the client happened to declare.
    [Fact]
    public async Task HandleAsync_NonGmAccount_ZeroLengthMac_SendsInvalidDevicesResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(
            ValidLoginRequest(password: "correct-password", physicalAddress: [], adapterName: "real-adapter-guid"),
            session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask, "Invalid devices!"));
        Assert.Null(session.AccountId);
    }

    // GM-tier accounts (grade one or above) are exempt from the gate entirely, even with an all-placeholder
    // device tuple -- the same account row was just overwritten with those placeholders as a side effect
    // (Server/ts25login/S08_MyDB.cpp:487-494), but that write never gates this check.
    [Fact]
    public async Task HandleAsync_GmAccount_ZeroLengthMac_StillSucceeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        // gmAllowlisted:true -- this test targets the anti-spoofing gate's GM exemption, not the separate
        // GM-IP allowlist gate (covered on its own below); an allowlisted IP keeps that gate out of the way.
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password", physicalAddress: []), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.NotNull(session.AccountSessionToken);
        // GM-BLOCK precondition: the account-grade fact must land on the session itself, not be re-derived later.
        Assert.Equal((short)1, session.AccountGrade);
    }

    // A non-zero-length MAC that is nonetheless the exact placeholder literal text still trips the gate --
    // the comparison is exact-text equality, not a byte-level "was this zero-length" test.
    [Fact]
    public async Task HandleAsync_NonGmAccount_MacEqualsPlaceholderLiteral_SendsInvalidDevicesResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(
            ValidLoginRequest(password: "correct-password", physicalAddress: ParseMac("00-00-00-00-00-00")),
            session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask, "Invalid devices!"));
    }

    // Placeholder adapter-name/GUID literal alone is sufficient -- the three conditions are "any one",
    // not "all three together".
    [Fact]
    public async Task HandleAsync_NonGmAccount_AdapterGuidEqualsPlaceholderLiteral_SendsInvalidDevicesResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(
            ValidLoginRequest(password: "correct-password", adapterName: "{0-0-0-0-0}"),
            session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask, "Invalid devices!"));
    }

    // A genuinely loopback connection trips the gate too, even with real MAC/GUID values -- a real boundary
    // condition the legacy check accepts (same-host non-GM connections are indistinguishable from spoofing).
    [Fact]
    public async Task HandleAsync_NonGmAccount_LoopbackRemoteIp_SendsInvalidDevicesResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var loopback = new IPEndPoint(IPAddress.Loopback, 40000);
        var handler = CreateHandler(out var session, out var pipe, accounts, remoteEndPoint: loopback);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask, "Invalid devices!"));
    }

    // Baseline: a non-GM account with a real, non-placeholder device tuple passes the gate silently and
    // proceeds exactly like HandleAsync_NoRestrictionsAndValidCredentials_Succeeds.
    [Fact]
    public async Task HandleAsync_NonGmAccount_RealDeviceTuple_Succeeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(
            ValidLoginRequest(password: "correct-password", adapterName: "{real-adapter-guid}"), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.NotNull(session.AccountSessionToken);
    }

    // Major audit gap (ts25extra-scope-confirmation cluster): the character-select roster used to always send
    // GuildName="" regardless of actual guild membership. LoginService now resolves each roster character's live
    // guild membership (IGuildRepository.GetByCharacterAsync) and LoginTrain.BuildAvatarSlots wires it onto the
    // wire GuildName field -- see LoginTrain.BuildAvatarSlots' own remarks for the full legacy citation.
    [Fact]
    public async Task HandleAsync_Success_PopulatesGuildNameForGuildedCharacter_AndLeavesGuildlessCharacterEmpty()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);

        var guildedCharacter = new CharacterSummaryDto(101, 0, "Hero", 2, 1, 3, 4, 12);
        var guildlessCharacter = new CharacterSummaryDto(102, 1, "Sidekick", 1, 0, 1, 2, 5);
        var characters = FakeCharacterRepository.WithSummaries(guildedCharacter, guildlessCharacter);

        var guilds = FakeGuildRepository.WithMembership(guildedCharacter.CharacterId,
            new CharacterGuildMembershipDto(55, "TestGuild", 0, ""));

        var handler = CreateHandler(out var session, out var pipe, accounts, characters: characters,
            guilds: guilds);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        var actual = await PacketAssert.ReadSentBytesAsync(pipe);

        var expectedSlots = LoginTrain.BuildAvatarSlots([guildedCharacter, guildlessCharacter],
            new Dictionary<int, string> { [guildedCharacter.CharacterId] = "TestGuild" });

        var loginRecvSize = FrameWriter.FrameSizeOf<LoginResponse>();
        var avatarSlotSize = FrameWriter.FrameSizeOf<AvatarRosterResponse>();

        var expectedSlot0Frame = new byte[avatarSlotSize];
        FrameWriter.WriteFrame(in expectedSlots[0], expectedSlot0Frame);
        var expectedSlot1Frame = new byte[avatarSlotSize];
        FrameWriter.WriteFrame(in expectedSlots[1], expectedSlot1Frame);

        Assert.Equal(expectedSlot0Frame, actual.AsSpan(loginRecvSize, avatarSlotSize).ToArray());
        Assert.Equal(expectedSlot1Frame, actual.AsSpan(loginRecvSize + avatarSlotSize, avatarSlotSize).ToArray());

        Assert.Equal("TestGuild", expectedSlots[0].GuildName);
        Assert.Equal("", expectedSlots[1].GuildName);
    }

    private static LoginHandler CreateHandler(out LoginClientSession session, out FakeDuplexPipe pipe,
        FakeAccountRepository accounts, bool blockedIp = false, bool firewallRuleBlocked = false,
        bool gmAllowlisted = false, bool accountBanned = false, string[]? bannedMacAddresses = null,
        SessionRegistry? registry = null, FakeAccountSessionRepository? accountSessions = null,
        IPEndPoint? remoteEndPoint = null, LoginCapacityState? capacity = null,
        FakeEventLogRepository? eventLog = null, bool onlyAdminCanLogin = false,
        FakeCharacterRepository? characters = null, FakeGuildRepository? guilds = null)
    {
        pipe = new FakeDuplexPipe();
        session = new LoginClientSession(1, pipe, remoteEndPoint ?? RemoteEndPoint);

        // Same fake instance backs both the generic block-list bypass (ApplicationFirewall) and the
        // GM-tier-specific post-authentication gate (LoginService) -- legacy checks the identical `gmip`
        // table for both (S04_MyWork02.cpp:195, S08_MyDB.cpp:339-356), so one allowlist fake is correct here.
        var gmAllowlistRepository = new FakeGmAllowlistRepository(gmAllowlisted);
        var firewall = new ApplicationFirewall(
            new FakeBlockedIpRepository(blockedIp),
            new FakeFirewallRuleRepository(firewallRuleBlocked),
            gmAllowlistRepository);

        return new LoginHandler(
            new LoginService(
                accounts,
                FakeAccountPinRepository.WithNoPin(),
                characters ?? FakeCharacterRepository.WithNone(),
                new LoginIpRateLimiter(),
                capacity ?? AllowedCapacity(),
                firewall,
                gmAllowlistRepository,
                new FakeBanRepository(accountBanned),
                new FakeMacRestrictionRepository(bannedMacAddresses ?? []),
                Options.Create(new LoginServerOptions
                    { ExpectedClientVersion = ClientVersion, OnlyAdminCanLogin = onlyAdminCanLogin }),
                registry ?? new SessionRegistry(),
                accountSessions ?? new FakeAccountSessionRepository(),
                eventLog ?? new FakeEventLogRepository(),
                guilds ?? FakeGuildRepository.Empty(),
                NullLogger<LoginService>.Instance),
            NullLogger<LoginHandler>.Instance);
    }

    /// <summary>
    ///     Comfortably large cap, never maintenance/full -- the default for every test that isn't
    ///     specifically exercising the capacity gates themselves.
    /// </summary>
    private static LoginCapacityState AllowedCapacity()
    {
        var state = new LoginCapacityState();
        state.SetMaxPlayers(10_000);
        state.SetCurrentPlayers(0);
        return state;
    }

    private static LoginRequest ValidLoginRequest(string id = "someuser", string password = "irrelevant",
        byte[]? physicalAddress = null, string adapterName = "")
    {
        var mac = physicalAddress ?? DefaultPhysicalAddress;
        return new LoginRequest
        {
            Id = id,
            Password = password,
            Version = ClientVersion,
            Adapter = new LoginAdapterInfo
            {
                AdapterName = adapterName,
                PhysicalAddressLength = (uint)mac.Length,
                PhysicalAddress = Pad8(mac),
                IPAddress = ""
            }
        };
    }

    private static byte[] Pad8(byte[] address)
    {
        var padded = new byte[8];
        address.AsSpan().CopyTo(padded);
        return padded;
    }

    private static byte[] ParseMac(string mac)
    {
        return mac.Split('-').Select(b => Convert.ToByte(b, 16)).ToArray();
    }

    /// <summary>
    ///     SqlException has no accessible public constructor -- it's only ever created by
    ///     Microsoft.Data.SqlClient itself from a real server round trip. Bypassing every constructor is the
    ///     only way to get an instance of the exact type LoginService's catch clause discriminates on; the
    ///     fake's Message/Number are irrelevant here since only the type, not the content, drives the branch
    ///     under test.
    /// </summary>
    private static SqlException CreateFakeSqlException()
    {
        return (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));
    }

    /// <summary>
    ///     Every outcome here sends the full 6-packet SEND_LOGIN train (LoginTrain.Send/SendFailure), so a plain
    ///     <see cref="PacketAssert.AssertSentAsync{TPacket}" /> (built for a single-packet reply) sees trailing
    ///     bytes from the later packets and fails on length alone. Only LC_LOGIN_RECV -- the sole packet whose
    ///     content actually depends on this cluster's new checks -- is asserted here; the rest of the train is
    ///     already covered by LoginTrainTests.
    /// </summary>
    private static async Task AssertFirstResponseAsync(FakeDuplexPipe pipe, LoginResponse expected)
    {
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        var expectedFrame = new byte[FrameWriter.FrameSizeOf<LoginResponse>()];
        FrameWriter.WriteFrame(in expected, expectedFrame);

        Assert.Equal(expectedFrame, actual.AsSpan(0, expectedFrame.Length).ToArray());
    }
}
