using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.Tests.World.WorldState;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeSymbolBattleSchedulerTests
{
    // Wednesday, chosen only because it's a stable, arbitrary reference day for these tests.
    private static readonly DateTime OpeningHour = new(2026, 7, 8, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ClosingHour = new(2026, 7, 8, 22, 0, 0, DateTimeKind.Utc);

    private static int OneFrame => FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();

    private static ZoneRegistry CreateRegistry(params short[] maps)
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options),
            new MovementRules(Options.Create(options)), new DirtyTracker<int>(), NullLogger<Zone>.Instance,
            ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize(maps);
        return registry;
    }

    private static WorldStateService CreateWorldState()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public void Tick_HourDoesNotMatch_StaysWaiting()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(1);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek> { OpeningHour.DayOfWeek });

        scheduler.Tick(TimeSpan.Zero, OpeningHour.AddHours(-1));

        Assert.Equal(TribeSymbolBattleSchedulePhase.WaitingForHour, scheduler.Phase);
    }

    [Fact]
    public void Tick_HourMatchesButWrongDay_NotTestMode_StaysWaiting()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(1);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance,
            new HashSet<DayOfWeek> { OpeningHour.DayOfWeek + 1 });

        scheduler.Tick(TimeSpan.Zero, OpeningHour);

        Assert.Equal(TribeSymbolBattleSchedulePhase.WaitingForHour, scheduler.Phase);
    }

    [Fact]
    public void Tick_HourAndDayMatch_EntersCountingPhase()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(1);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek> { OpeningHour.DayOfWeek });

        scheduler.Tick(TimeSpan.Zero, OpeningHour);

        Assert.Equal(TribeSymbolBattleSchedulePhase.Counting, scheduler.Phase);
    }

    [Fact]
    public void Tick_TestMode_BypassesDayGate_ButHourGateStillApplies()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(1);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek>(), true);

        // Wrong day, but test mode -- the hour still matches, so it must arm.
        scheduler.Tick(TimeSpan.Zero, OpeningHour);
        Assert.Equal(TribeSymbolBattleSchedulePhase.Counting, scheduler.Phase);

        var other = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek>(), true);
        other.Tick(TimeSpan.Zero, OpeningHour.AddHours(1));
        Assert.Equal(TribeSymbolBattleSchedulePhase.WaitingForHour, other.Phase);
    }

    [Fact]
    public void FullOpeningCycle_TenCountdownPulses_ThenFlagFlipsAfterTheEleventhMinute()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        registry[1].Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        registry[1].Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek> { OpeningHour.DayOfWeek });

        scheduler.Tick(TimeSpan.Zero, OpeningHour);
        Assert.Equal(TribeSymbolBattleSchedulePhase.Counting, scheduler.Phase);

        var pulses = 0;
        for (var minute = 1; minute <= 10; minute++)
        {
            scheduler.Tick(TimeSpan.FromMinutes(1), OpeningHour);
            var frame = ZoneTestKit.DrainOutbound(pipe);
            Assert.Equal(OneFrame, frame.Length);
            Assert.Equal(39, BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1)));
            pulses++;
        }

        Assert.Equal(10, pulses);
        Assert.False(worldState.World.TribeSymbolBattle); // not open yet -- one more minute of grace remains

        // 11th minute: the grace minute -- flips the flag and broadcasts sort 40, not another sort-39 pulse.
        scheduler.Tick(TimeSpan.FromMinutes(1), OpeningHour);

        Assert.True(worldState.World.TribeSymbolBattle);
        Assert.Equal(TribeSymbolBattleSchedulePhase.WaitingForHour, scheduler.Phase);
        var finalFrame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(finalFrame.AsSpan(1)));
    }

    [Fact]
    public void FullClosingCycle_UsesCloseHour_AndFlipsFlagBackToFalse()
    {
        var worldState = CreateWorldState();
        worldState.StartTribeSymbolBattle();
        var registry = CreateRegistry(1);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek> { ClosingHour.DayOfWeek });

        // The opening hour must NOT arm it while the flag already reads open.
        scheduler.Tick(TimeSpan.Zero, OpeningHour);
        Assert.Equal(TribeSymbolBattleSchedulePhase.WaitingForHour, scheduler.Phase);

        scheduler.Tick(TimeSpan.Zero, ClosingHour);
        Assert.Equal(TribeSymbolBattleSchedulePhase.Counting, scheduler.Phase);

        for (var minute = 1; minute <= 11; minute++)
            scheduler.Tick(TimeSpan.FromMinutes(1), ClosingHour);

        Assert.False(worldState.World.TribeSymbolBattle);
        Assert.Equal(TribeSymbolBattleSchedulePhase.WaitingForHour, scheduler.Phase);
    }

    [Fact]
    public void MinutesRemainingInCountdown_CountsDownFromTenToOne()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(1);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek> { OpeningHour.DayOfWeek });

        scheduler.Tick(TimeSpan.Zero, OpeningHour);
        Assert.Equal(0, scheduler.MinutesRemainingInCountdown); // armed, but no minute has elapsed yet

        scheduler.Tick(TimeSpan.FromMinutes(1), OpeningHour);
        Assert.Equal(10, scheduler.MinutesRemainingInCountdown);

        scheduler.Tick(TimeSpan.FromMinutes(1), OpeningHour);
        Assert.Equal(9, scheduler.MinutesRemainingInCountdown);
    }

    [Fact]
    public void RepeatedMatchingHour_AcrossConsecutiveTicks_DoesNotDoubleArm()
    {
        var worldState = CreateWorldState();
        var registry = CreateRegistry(1);
        var broadcaster = new ZoneEventBroadcaster(worldState, registry, NullLogger<ZoneEventBroadcaster>.Instance);
        var scheduler = new TribeSymbolBattleScheduler(worldState, broadcaster,
            NullLogger<TribeSymbolBattleScheduler>.Instance, new HashSet<DayOfWeek> { OpeningHour.DayOfWeek });

        scheduler.Tick(TimeSpan.Zero, OpeningHour);
        scheduler.Tick(TimeSpan.Zero, OpeningHour.AddMinutes(1));
        scheduler.Tick(TimeSpan.Zero, OpeningHour.AddMinutes(2));

        // Still just one countdown in progress -- MinutesElapsed only advances via the elapsed-time accumulator,
        // never re-armed by the still-matching wall-clock hour on subsequent ticks.
        Assert.Equal(TribeSymbolBattleSchedulePhase.Counting, scheduler.Phase);
        Assert.Equal(0, scheduler.MinutesRemainingInCountdown);
    }
}
