using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Hosting;
using Fenrir.LoginServer.Tests.TestSupport;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.LoginServer.Tests;

public class GreetingCapacityPacketTests
{
    [Fact]
    public void BuildGreetingPacket_ReflectsCurrentCapacityStateSnapshot()
    {
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(250);
        capacity.SetGagePlayers(77);
        capacity.SetCurrentPlayers(37);
        var host = CreateHost(capacity);

        var packet = host.BuildGreetingPacket(12345);

        Assert.Equal(12345, packet.RandomNumber);
        Assert.Equal(250, packet.MaxPlayerNum);
        Assert.Equal(77, packet.GagePlayerNum);
        Assert.Equal(37, packet.PresentPlayerNum);
    }

    [Fact]
    public void BuildGreetingPacket_ReflectsMaintenanceModeToggleWithoutRestart()
    {
        var capacity = new LoginCapacityState();
        capacity.SetMaxPlayers(1000);
        var host = CreateHost(capacity);

        Assert.Equal(1000, host.BuildGreetingPacket(1).MaxPlayerNum);

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

        Assert.Equal(capacity.MaxPlayers, packet.MaxPlayerNum);
        Assert.Equal(capacity.CurrentPlayers, packet.PresentPlayerNum);
        Assert.Equal(LoginCapacityOutcome.Allowed,
            LoginCapacityGate.Evaluate(packet.MaxPlayerNum, packet.PresentPlayerNum));
    }

    private static LoginConnectionHost CreateHost(LoginCapacityState capacity)
    {
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
