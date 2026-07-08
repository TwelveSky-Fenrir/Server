using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Hosting;
using Fenrir.LoginServer.Tests.TestSupport;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.LoginServer.Tests;

// Major-gap fix (server-list-population-capacity audit): the connect-time greeting used to source
// MaxPlayerNum from a static Login:MaxPlayerNum config value bound once at process startup, and
// PresentPlayerNum from a separate CCU-sum query over runtime.GameServerDirectory -- both entirely
// independent of LoginCapacityState, the continuously (~1s) refreshed snapshot LoginCapacityGate.Evaluate
// actually reads on every login attempt (LoginService.LoginAsync). LoginConnectionHost.BuildGreetingPacket
// now reads LoginCapacityState directly for both figures, so a client that merely connects (before ever
// attempting to log in) sees the exact same maintenance/full-server picture the gate will enforce, and a
// live admin.ServerQuota.MaxPlayers change (e.g. flipping into maintenance mode) is visible on the very
// next accepted connection with no server restart. Supersedes the former ReadLivePlayerCountAsyncTests
// (the CCU-sum path it exercised no longer exists).
public class GreetingCapacityPacketTests
{
    [Fact]
    public void BuildGreetingPacket_ReflectsCurrentCapacityStateSnapshot()
    {
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(250);
        capacity.SetCurrentPlayers(37);
        var host = CreateHost(capacity);

        var packet = host.BuildGreetingPacket(12345);

        Assert.Equal(12345, packet.RandomNumber);
        Assert.Equal(250, packet.MaxPlayerNum);
        Assert.Equal(37, packet.PresentPlayerNum);
        Assert.Equal(0, packet.GagePlayerNum);
    }

    [Fact]
    public void BuildGreetingPacket_ReflectsMaintenanceModeToggleWithoutRestart()
    {
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(1000);
        var host = CreateHost(capacity);

        Assert.Equal(1000, host.BuildGreetingPacket(1).MaxPlayerNum);

        // Simulates ServerQuotaRefreshHost's next ~1s tick observing admin.ServerQuota.MaxPlayers flipped to
        // 0 (maintenance) -- same process, same LoginCapacityState instance, no restart, no re-injection.
        capacity.SetMaxPlayers(0);

        Assert.Equal(0, host.BuildGreetingPacket(2).MaxPlayerNum);
    }

    [Fact]
    public void BuildGreetingPacket_PresentAndMaxPlayerNumAlwaysAgreeWithTheCapacityGate()
    {
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(100);
        capacity.SetCurrentPlayers(64);
        var host = CreateHost(capacity);

        var packet = host.BuildGreetingPacket(1);

        // Regression test for the audit's core complaint: the greeting and LoginCapacityGate.Evaluate must
        // never look at two different numbers. Both LoginService.LoginAsync (the gate) and this packet now
        // read the exact same LoginCapacityState instance/properties.
        Assert.Equal(capacity.MaxPlayers, packet.MaxPlayerNum);
        Assert.Equal(capacity.CurrentPlayers, packet.PresentPlayerNum);
        Assert.Equal(LoginCapacityOutcome.Allowed,
            LoginCapacityGate.Evaluate(packet.MaxPlayerNum, packet.PresentPlayerNum));
    }

    private static LoginConnectionHost CreateHost(LoginCapacityState capacity)
    {
        // dispatcher/opcodeRegistry/rateLimiter are never touched by BuildGreetingPacket -- only Greet()'s I/O-pump
        // continuation (not exercised here) would need them.
        return new LoginConnectionHost(
            Options.Create(new LoginServerOptions()),
            null!,
            null!,
            null!,
            new SessionRegistry(),
            capacity,
            new FakeAccountSessionRepository(),
            new FakeEventLogRepository(),
            new IpFloodGuard(int.MaxValue, int.MaxValue, static (_, _) => ValueTask.CompletedTask,
                new SessionRegistry()),
            NullLogger<LoginConnectionHost>.Instance);
    }
}
