using System.Net;
using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Application.Login.RateLimiting;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Security;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Tests.Handlers;

// op11 CL_LOGIN_SEND -- cluster C02: application firewall (IP block/GM-allowlist bypass), MAC-restriction
// ("banned PC"), and the admin.Bans ban-log check layered on top of the pre-existing account.IsBanned flag.
public class LoginHandlerTests
{
    private const int ClientVersion = 90354; // LoginServerOptions.ExpectedClientVersion default
    private static readonly IPEndPoint RemoteEndPoint = new(IPAddress.Parse("203.0.113.50"), 40000);

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
    }

    private static LoginHandler CreateHandler(out LoginClientSession session, out FakeDuplexPipe pipe,
        FakeAccountRepository accounts, bool blockedIp = false, bool firewallRuleBlocked = false,
        bool gmAllowlisted = false, bool accountBanned = false, params string[] bannedMacAddresses)
    {
        pipe = new FakeDuplexPipe();
        session = new LoginClientSession(1, pipe, RemoteEndPoint);

        var firewall = new ApplicationFirewall(
            new FakeBlockedIpRepository(blockedIp),
            new FakeFirewallRuleRepository(firewallRuleBlocked),
            new FakeGmAllowlistRepository(gmAllowlisted));

        return new LoginHandler(new LoginService(
            accounts,
            FakeAccountPinRepository.WithNoPin(),
            FakeCharacterRepository.WithNone(),
            new LoginIpRateLimiter(),
            firewall,
            new FakeBanRepository(accountBanned),
            new FakeMacRestrictionRepository(bannedMacAddresses),
            Options.Create(new LoginServerOptions { ExpectedClientVersion = ClientVersion }),
            new SessionRegistry()));
    }

    private static LoginRequest ValidLoginRequest(string id = "someuser", string password = "irrelevant",
        byte[]? physicalAddress = null)
    {
        return new LoginRequest
        {
            Id = id,
            Password = password,
            Version = ClientVersion,
            Adapter = new LoginAdapterInfo
            {
                AdapterName = "",
                PhysicalAddressLength = physicalAddress is null ? 0u : (uint)physicalAddress.Length,
                PhysicalAddress = Pad8(physicalAddress ?? []),
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
