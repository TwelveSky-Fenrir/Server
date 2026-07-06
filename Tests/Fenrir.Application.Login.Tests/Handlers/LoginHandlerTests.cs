using System.Net;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.RateLimiting;
using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.Login;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Security;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Handlers;

// op11 CL_LOGIN_SEND -- cluster C02: application firewall (IP block/GM-allowlist bypass), MAC-restriction
// ("banned PC"), and the admin.Bans ban-log check layered on top of the pre-existing account.IsBanned flag.
public class LoginHandlerTests
{
    private const int ClientVersion = 90354; // LoginServerOptions.ExpectedClientVersion default
    private static readonly IPEndPoint RemoteEndPoint = new(IPAddress.Parse("203.0.113.50"), 40000);

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
        var handler = CreateHandler(out var session, out var pipe, accounts, blockedIp: true, capacity: capacity);

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
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false);
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
            blockedIp: true);

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(2, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Equal(0, accounts.AuthenticateCallCount);
    }

    [Fact]
    public async Task HandleAsync_IpOnGmAllowlist_BypassesTheBlockedIpList_AndProceedsToAuthenticate()
    {
        var accounts = FakeAccountRepository.WithNoAccount();
        var handler = CreateHandler(out var session, out var pipe, accounts,
            blockedIp: true, gmAllowlisted: true);

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
        var account = new AuthenticateAccountDto(1, hash, salt, 0, null, IsBanned: false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, accountBanned: true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(9, "someuser", 0, LoginTrain.FailurePinMask));
    }

    [Fact]
    public async Task HandleAsync_NoRestrictionsAndValidCredentials_Succeeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false);
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
    public async Task HandleAsync_ConflictLogin_WithALiveLocalSession_EvictsTheOldSession_AndDropsTheNewAttemptSilently()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var registry = new SessionRegistry();
        var staleSession = new LoginClientSession(99, new FakeDuplexPipe());
        registry.Register(staleSession);
        registry.AssociateAccount(99, 7);

        var accountSessions = new FakeAccountSessionRepository { ClaimOutcome = AccountSessionClaimOutcome.ConflictLogin };
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
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var accountSessions = new FakeAccountSessionRepository { ClaimOutcome = AccountSessionClaimOutcome.ConflictLogin };
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
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var accountSessions = new FakeAccountSessionRepository { ClaimOutcome = outcome };
        var handler = CreateHandler(out var session, out var pipe, accounts, accountSessions: accountSessions);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(8, "someuser", 0, LoginTrain.FailurePinMask));
    }

    // "Protect Spoofed" anti-spoofing gate (Server/ts25login/S08_MyDB.cpp:497-507): a non-GM account whose
    // declared MAC is zero-length never gets its device values populated with real data at all, so the gate
    // always trips regardless of whatever GUID/IP the client happened to declare.
    [Fact]
    public async Task HandleAsync_NonGmAccount_ZeroLengthMac_SendsInvalidDevicesResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false, AccountGrade: 0);
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
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false, AccountGrade: 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

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
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false, AccountGrade: 0);
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
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false, AccountGrade: 0);
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
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false, AccountGrade: 0);
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
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, IsBanned: false, AccountGrade: 0);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(
            ValidLoginRequest(password: "correct-password", adapterName: "{real-adapter-guid}"), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.NotNull(session.AccountSessionToken);
    }

    private static LoginHandler CreateHandler(out LoginClientSession session, out FakeDuplexPipe pipe,
        FakeAccountRepository accounts, bool blockedIp = false, bool firewallRuleBlocked = false,
        bool gmAllowlisted = false, bool accountBanned = false, string[]? bannedMacAddresses = null,
        SessionRegistry? registry = null, FakeAccountSessionRepository? accountSessions = null,
        IPEndPoint? remoteEndPoint = null, LoginCapacityState? capacity = null,
        FakeEventLogRepository? eventLog = null)
    {
        pipe = new FakeDuplexPipe();
        session = new LoginClientSession(1, pipe, remoteEndPoint ?? RemoteEndPoint);

        var firewall = new ApplicationFirewall(
            new FakeBlockedIpRepository(blockedIp),
            new FakeFirewallRuleRepository(firewallRuleBlocked),
            new FakeGmAllowlistRepository(gmAllowlisted));

        return new LoginHandler(new LoginService(
            accounts,
            FakeAccountPinRepository.WithNoPin(),
            FakeCharacterRepository.WithNone(),
            new LoginIpRateLimiter(),
            capacity ?? AllowedCapacity(),
            firewall,
            new FakeBanRepository(accountBanned),
            new FakeMacRestrictionRepository(bannedMacAddresses ?? []),
            Options.Create(new LoginServerOptions { ExpectedClientVersion = ClientVersion }),
            registry ?? new SessionRegistry(),
            accountSessions ?? new FakeAccountSessionRepository(),
            eventLog ?? new FakeEventLogRepository()));
    }

    /// <summary>Comfortably large cap, never maintenance/full -- the default for every test that isn't
    /// specifically exercising the capacity gates themselves.</summary>
    private static LoginCapacityState AllowedCapacity()
    {
        var state = new LoginCapacityState();
        state.SetMaxPlayers(10_000);
        state.SetCurrentPlayers(0);
        return state;
    }

    // Realistic, non-placeholder default device tuple: a real client always has a non-zero-length MAC, so
    // the anti-spoofing gate (cluster C09, DeviceSpoofingGuard) never trips unless a test deliberately
    // asks for the zero-length-MAC or placeholder-literal edge cases it covers below.
    private static readonly byte[] DefaultPhysicalAddress = ParseMac("11-22-33-44-55-66");

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
