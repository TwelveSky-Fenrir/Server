using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="MountExpiryCountdownSystem" />: the once-per-real-minute expired-mount auto-dismount
///     (Server/ts25zone/S07_MyGame03.cpp:6360-6363). The system deliberately does NOT decrement the mount timer
///     (the per-minute decrement is uncited -- see the system's own remarks); these tests drive an
///     already-expired mounted state and assert the check-and-dismount.
/// </summary>
public class MountExpiryCountdownSystemTests
{
    private static readonly TimeSpan OneMinute =
        SimulationClock.ToTimeSpan(SimulationClock.PlayTimeAccrualLegacyTicks);

    private const short PlainZone = 100;

    private static (Zone Zone, PlayerRuntimeState State) EnterPlayer()
    {
        var zone = ZoneTestKit.CreateZone(PlainZone, simulationSystems: [new MountExpiryCountdownSystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, PlainZone)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!);
    }

    [Fact]
    public void ExpiredMountedPlayer_IsForceDismounted_AfterOneMinute()
    {
        var (zone, state) = EnterPlayer();
        state.AnimalIndex = 12; // mounted band, garage slot 2
        state.AnimalNumber = 1301;
        state.AnimalAbsorbState = 1;
        state.AnimalTime = 0; // rental time run out

        zone.Tick(OneMinute);

        Assert.Equal(2, state.AnimalIndex); // left the mounted band (12 - 10)
        Assert.Equal(0, state.AnimalNumber);
        Assert.Equal(0, state.AnimalAbsorbState);
        Assert.True(state.MountAutoDismountPending);
    }

    [Fact]
    public void MountedPlayerWithTimeRemaining_IsUntouched()
    {
        var (zone, state) = EnterPlayer();
        state.AnimalIndex = 12;
        state.AnimalNumber = 1301;
        state.AnimalTime = 5;

        zone.Tick(OneMinute);

        Assert.Equal(12, state.AnimalIndex);
        Assert.Equal(1301, state.AnimalNumber);
        Assert.False(state.MountAutoDismountPending);
    }

    [Fact]
    public void UnmountedPlayer_IsUntouched()
    {
        var (zone, state) = EnterPlayer();
        state.AnimalIndex = -1; // no mount selected
        state.AnimalTime = 0;

        zone.Tick(OneMinute);

        Assert.Equal(-1, state.AnimalIndex);
        Assert.False(state.MountAutoDismountPending);
    }

    [Fact]
    public void BeforeAFullMinute_TheCheckDoesNotRun()
    {
        var (zone, state) = EnterPlayer();
        state.AnimalIndex = 12;
        state.AnimalTime = 0;

        zone.Tick(TimeSpan.FromSeconds(30)); // half a minute -- below the cadence boundary

        Assert.Equal(12, state.AnimalIndex);
        Assert.False(state.MountAutoDismountPending);
    }
}
