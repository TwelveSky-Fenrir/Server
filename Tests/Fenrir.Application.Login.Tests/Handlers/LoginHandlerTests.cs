using System.Net;
using System.Runtime.CompilerServices;
using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Domain.RateLimiting;
using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.Login;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Security;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Handlers;

public class LoginHandlerTests
{
    private const int ClientVersion = 90354;

    private const string DefaultAdapterName = "{real-adapter-guid}";
    private static readonly IPEndPoint RemoteEndPoint = new(IPAddress.Parse("203.0.113.50"), 40000);

    private static readonly byte[] DefaultPhysicalAddress = ParseMac("11-22-33-44-55-66");

    [Fact]
    public async Task HandleAsync_IpExceedsRateLimitBudget_SendsCustomMessageResult_InsteadOfSilentlyDropping()
    {
        var accounts = FakeAccountRepository.WithNoAccount();
        var handler = CreateHandler(out var session, out var pipe, accounts);

        for (var i = 0; i < 5; i++)
        {
            await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);
            await PacketAssert.ReadSentBytesAsync(pipe);
        }

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask,
                "Too many login attempts from your IP. Please try again later."));
        Assert.Null(session.AccountId);
    }

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
    public async Task HandleAsync_ValidCredentials_IpIsBlocked_SendsIpBlockedResult_AfterAuthenticating()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts,
            true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(2, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Equal(1, accounts.AuthenticateCallCount);
        Assert.Null(session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_UnknownAccount_IpIsBlocked_SendsUnknownAccountResult_NotIpBlockedResult()
    {
        var accounts = FakeAccountRepository.WithNoAccount();
        var handler = CreateHandler(out var session, out var pipe, accounts,
            true);

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(6, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Equal(1, accounts.AuthenticateCallCount);
    }

    [Fact]
    public async Task HandleAsync_WrongPassword_IpIsBlocked_SendsWrongPasswordResult_NotIpBlockedResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts,
            true);

        await handler.HandleAsync(ValidLoginRequest(password: "totally-wrong-password"), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(7, "someuser", 0, LoginTrain.FailurePinMask));
    }

    [Fact]
    public async Task HandleAsync_IpOnGmAllowlist_BypassesTheBlockedIpList_AndProceedsToAuthenticate()
    {
        var accounts = FakeAccountRepository.WithNoAccount();
        var handler = CreateHandler(out var session, out var pipe, accounts,
            true, gmAllowlisted: true);

        await handler.HandleAsync(ValidLoginRequest(), session, CancellationToken.None);

        Assert.Equal(1, accounts.AuthenticateCallCount);
        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(6, "someuser", 0, LoginTrain.FailurePinMask));
    }

    [Fact]
    public async Task HandleAsync_MacAddressIsBanned_SendsCustomMessageResult()
    {
        const string bannedMac = "00-11-22-33-44-55";
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts,
            bannedMacAddresses: [bannedMac]);

        await handler.HandleAsync(
            ValidLoginRequest(password: "correct-password", physicalAddress: ParseMac(bannedMac)), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask, "Your PC has been banned."));
        Assert.Null(session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_GmAccount_MacAddressIsBanned_StillSucceeds()
    {
        const string bannedMac = "00-11-22-33-44-55";
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: true,
            bannedMacAddresses: [bannedMac]);

        await handler.HandleAsync(
            ValidLoginRequest(password: "correct-password", physicalAddress: ParseMac(bannedMac)), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
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
    public async Task
        HandleAsync_AccountUnderAutoLockoutTimer_SendsCustomMessageResult_NotTheAdminBanResultCode()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(1, hash, salt, 10, DateTime.UtcNow.AddMinutes(15), false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask,
                "Too many failed login attempts. Please try again later."));
        Assert.Null(session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_NoRestrictionsAndValidCredentials_Succeeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.NotNull(session.AccountSessionToken);
        Assert.Equal((short)0, session.AccountGrade);
    }

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
        Assert.Equal(LoginSessionState.VersionOk, session.State);
    }

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
        Assert.Equal(LoginSessionState.VersionOk, session.State);
    }

    [Fact]
    public async Task
        HandleAsync_GmAccount_WrongPassword_NonAllowlistedIp_SendsIpBlockedResult_NotWrongPasswordResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: false);

        await handler.HandleAsync(ValidLoginRequest(password: "totally-wrong-password"), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(2, "someuser", 0, LoginTrain.FailurePinMask));
        Assert.Null(session.AccountId);
        Assert.Null(accounts.LastRecordedAttempt);
        Assert.Equal(LoginSessionState.VersionOk, session.State);
    }

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

    [Fact]
    public async Task HandleAsync_NonGmAccount_EmptyAdapterName_SendsCustomMessageResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password", adapterName: ""), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask,
                "IP address not specified. Please update to the latest client."));
        Assert.Null(session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_GmAccount_EmptyAdapterName_SendsCustomMessageResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password", adapterName: ""), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask,
                "IP address not specified. Please update to the latest client."));
        Assert.Null(session.AccountId);
    }

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

    [Fact]
    public async Task HandleAsync_GmAccount_ZeroLengthMac_StillSucceeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: true);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password", physicalAddress: []), session,
            CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.NotNull(session.AccountSessionToken);
        Assert.Equal((short)1, session.AccountGrade);
    }

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

    [Fact]
    public async Task HandleAsync_NonGmAccount_ConfiguredAdapterLimitOfExactlyOne_SendsConnectionLimitExceededResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var mac = MacAddressFormatter.Format(DefaultPhysicalAddress, (uint)DefaultPhysicalAddress.Length);
        var handler = CreateHandler(out var session, out var pipe, accounts,
            configuredAccountLimits: new Dictionary<string, int> { [mac] = 1 });

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask,
                "The connection limit from your PC has been exceeded."));
        Assert.Null(session.AccountId);
    }

    [Fact]
    public async Task HandleAsync_NonGmAccount_ConfiguredAdapterLimitOfTwoOrMore_StillSucceeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var mac = MacAddressFormatter.Format(DefaultPhysicalAddress, (uint)DefaultPhysicalAddress.Length);
        var handler = CreateHandler(out var session, out var pipe, accounts,
            configuredAccountLimits: new Dictionary<string, int> { [mac] = 2 });

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
    }

    [Fact]
    public async Task HandleAsync_GmAccount_ConfiguredAdapterLimitOfExactlyOne_StillSucceeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var mac = MacAddressFormatter.Format(DefaultPhysicalAddress, (uint)DefaultPhysicalAddress.Length);
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: true,
            configuredAccountLimits: new Dictionary<string, int> { [mac] = 1 });

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
    }

    [Fact]
    public async Task
        HandleAsync_NonGmAccount_NoConfiguredRestriction_TwoConcurrentDeviceSessions_SendsConnectionLimitExceededResult()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var accountSessions = new FakeAccountSessionRepository { ConcurrentDeviceSessionCount = 2 };
        var handler = CreateHandler(out var session, out var pipe, accounts, accountSessions: accountSessions);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe,
            LoginTrain.BuildLoginRecv(10000, "someuser", 0, LoginTrain.FailurePinMask,
                "The connection limit from your PC has been exceeded."));
        Assert.Null(session.AccountId);
        Assert.Equal((7, "{real-adapter-guid}", "", "203.0.113.50"), accountSessions.LastConcurrentDeviceSessionQuery);
        Assert.Null(accountSessions.LastRecordedDeviceSignature);
    }

    [Fact]
    public async Task
        HandleAsync_NonGmAccount_NoConfiguredRestriction_OneConcurrentDeviceSession_Succeeds_AndRecordsTheRealDeviceSignature()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);
        var accountSessions = new FakeAccountSessionRepository { ConcurrentDeviceSessionCount = 1 };
        var handler = CreateHandler(out var session, out var pipe, accounts, accountSessions: accountSessions);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.Equal((7, "{real-adapter-guid}", "", "203.0.113.50"), accountSessions.LastRecordedDeviceSignature);
    }

    [Fact]
    public async Task HandleAsync_GmAccount_NoConfiguredRestriction_TwoConcurrentDeviceSessions_StillSucceeds()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false, 1);
        var accounts = FakeAccountRepository.WithAccount(account);
        var accountSessions = new FakeAccountSessionRepository { ConcurrentDeviceSessionCount = 2 };
        var handler = CreateHandler(out var session, out var pipe, accounts, gmAllowlisted: true,
            accountSessions: accountSessions);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        await AssertFirstResponseAsync(pipe, LoginTrain.BuildLoginRecv(0, "MG7", 1, ""));
        Assert.Null(accountSessions.LastConcurrentDeviceSessionQuery);
        Assert.Equal((7, "{0-0-0-0-0}", "127.0.0.1", "127.0.0.1"), accountSessions.LastRecordedDeviceSignature);
    }

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

        var expectedSlots = LoginTrain.BuildAvatarSlots([
            new AvatarRosterEntry(ToRosterDto(guildedCharacter), [], "TestGuild",
                new Dictionary<byte, string>(), "", ""),
            new AvatarRosterEntry(ToRosterDto(guildlessCharacter), [], "",
                new Dictionary<byte, string>(), "", "")
        ]);

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

    [Fact]
    public async Task HandleAsync_Success_PopulatesEquipmentAndProgressionScalarsFromTheAccountRosterRead()
    {
        var (hash, salt) = PasswordHasher.Hash("correct-password");
        var account = new AuthenticateAccountDto(7, hash, salt, 0, null, false);
        var accounts = FakeAccountRepository.WithAccount(account);

        var summary = new CharacterSummaryDto(201, 0, "Hero", 2, 1, 3, 4, 12);
        var richCharacter = new CharacterRosterDto(
            201, 0, "Hero", 2, 1, 1, 3,
            4, 12, 3, 5, 2, 40, 7,
            1, 2, 3, 4, 5,
            10, 50, 6, 1f, 2f, 3f, 30, 21);
        var equippedWeapon = new CharacterRosterItemDto(201, 2, 7, 9001, 1,
            0, 0, 0, 0, 0, 0, 0,
            0, 1);

        var characters = FakeCharacterRepository.WithSummaries(summary)
            .WithRosterCharacter(richCharacter)
            .WithRosterItem(equippedWeapon);

        var handler = CreateHandler(out var session, out var pipe, accounts, characters: characters);

        await handler.HandleAsync(ValidLoginRequest(password: "correct-password"), session, CancellationToken.None);

        var actual = await PacketAssert.ReadSentBytesAsync(pipe);

        var expectedSlots = LoginTrain.BuildAvatarSlots([
            new AvatarRosterEntry(richCharacter, [equippedWeapon], "", new Dictionary<byte, string>(), "", "")
        ]);

        var loginRecvSize = FrameWriter.FrameSizeOf<LoginResponse>();
        var avatarSlotSize = FrameWriter.FrameSizeOf<AvatarRosterResponse>();

        var expectedSlot0Frame = new byte[avatarSlotSize];
        FrameWriter.WriteFrame(in expectedSlots[0], expectedSlot0Frame);

        Assert.Equal(expectedSlot0Frame, actual.AsSpan(loginRecvSize, avatarSlotSize).ToArray());

        Assert.Equal(9001, expectedSlots[0].Equip[7 * 4]);
        Assert.Equal(5, expectedSlots[0].Halo);
        Assert.Equal(2, expectedSlots[0].RebirthNum);
        Assert.Equal(40, expectedSlots[0].KillOtherTribe);
        Assert.Equal(7, expectedSlots[0].SkillPoint);
        Assert.Equal(1, expectedSlots[0].PreviousTribe);
    }

    private static LoginHandler CreateHandler(out LoginClientSession session, out FakeDuplexPipe pipe,
        FakeAccountRepository accounts, bool blockedIp = false, bool firewallRuleBlocked = false,
        bool gmAllowlisted = false, bool accountBanned = false, string[]? bannedMacAddresses = null,
        IReadOnlyDictionary<string, int>? configuredAccountLimits = null,
        SessionRegistry? registry = null, FakeAccountSessionRepository? accountSessions = null,
        IPEndPoint? remoteEndPoint = null, LoginCapacityState? capacity = null,
        FakeEventLogRepository? eventLog = null, bool onlyAdminCanLogin = false,
        FakeCharacterRepository? characters = null, FakeGuildRepository? guilds = null,
        FakeFriendRepository? friends = null, FakeMentorRepository? mentor = null)
    {
        pipe = new FakeDuplexPipe();
        session = new LoginClientSession(1, pipe, remoteEndPoint ?? RemoteEndPoint);

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
                new FakeMacRestrictionRepository(bannedMacAddresses, configuredAccountLimits),
                Options.Create(new LoginServerOptions
                    { ExpectedClientVersion = ClientVersion, OnlyAdminCanLogin = onlyAdminCanLogin }),
                registry ?? new SessionRegistry(),
                accountSessions ?? new FakeAccountSessionRepository(),
                eventLog ?? new FakeEventLogRepository(),
                guilds ?? FakeGuildRepository.Empty(),
                friends ?? FakeFriendRepository.Empty(),
                mentor ?? FakeMentorRepository.Empty(),
                NullLogger<LoginService>.Instance),
            NullLogger<LoginHandler>.Instance);
    }

    private static LoginCapacityState AllowedCapacity()
    {
        var state = new LoginCapacityState();
        state.SetMaxPlayers(10_000);
        state.SetCurrentPlayers(0);
        return state;
    }

    private static LoginRequest ValidLoginRequest(string id = "someuser", string password = "irrelevant",
        byte[]? physicalAddress = null, string adapterName = DefaultAdapterName)
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

    private static CharacterRosterDto ToRosterDto(CharacterSummaryDto summary)
    {
        return new CharacterRosterDto(
            summary.CharacterId,
            summary.Slot,
            summary.Name,
            summary.Tribe,
            0,
            summary.Gender,
            summary.HeadType,
            summary.FaceType,
            summary.Level,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0f,
            0f,
            0f,
            0,
            0);
    }

    private static SqlException CreateFakeSqlException()
    {
        return (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));
    }

    private static async Task AssertFirstResponseAsync(FakeDuplexPipe pipe, LoginResponse expected)
    {
        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        var expectedFrame = new byte[FrameWriter.FrameSizeOf<LoginResponse>()];
        FrameWriter.WriteFrame(in expected, expectedFrame);

        Assert.Equal(expectedFrame, actual.AsSpan(0, expectedFrame.Length).ToArray());
    }
}
