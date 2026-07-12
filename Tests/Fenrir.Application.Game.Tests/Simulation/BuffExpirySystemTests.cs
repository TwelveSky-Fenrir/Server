using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

public class BuffExpirySystemTests
{
    private static readonly TimeSpan OneGateOpening =
        SimulationClock.ToTimeSpan(SimulationClock.OneSecondGateLegacyTicks);

    [Fact]
    public void Simulate_DecrementsDurationByOnePerGateOpening()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[0] = 25;
        state.Buffs.Buff[1] = 3;

        zone.Tick(OneGateOpening);

        Assert.Equal(25, state.Buffs.Buff[0]);
        Assert.Equal(2, state.Buffs.Buff[1]);
    }

    [Fact]
    public void Simulate_DurationReachingZero_ClearsBothValueAndDuration()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[10 * 2] = 40;
        state.Buffs.Buff[10 * 2 + 1] = 1;

        zone.Tick(OneGateOpening);

        Assert.Equal(0, state.Buffs.Buff[10 * 2]);
        Assert.Equal(0, state.Buffs.Buff[10 * 2 + 1]);
    }

    [Fact]
    public void Simulate_DurationReachingZero_MarksSlotAsExpiredNotActivated()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[10 * 2] = 40;
        state.Buffs.Buff[10 * 2 + 1] = 1;

        zone.Tick(OneGateOpening);

        Assert.Equal(2, state.BuffChangeScratch[10]);
    }

    [Fact]
    public void Simulate_UnoccupiedSlot_IsNeverTouched()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));

        zone.Tick(OneGateOpening);

        Assert.All(state!.Buffs.Buff, v => Assert.Equal(0, v));
    }

    [Fact]
    public void Simulate_BurstOfMultipleLegacyTicks_DecrementsByWholeGateOpenings()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[0] = 10;
        state.Buffs.Buff[1] = 10;

        zone.Tick(SimulationClock.ToTimeSpan(12));

        Assert.Equal(4, state.Buffs.Buff[1]);
    }

    [Fact]
    public void Simulate_Slot15Expiry_ClearsDarkAttackKind()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[15 * 2] = 40;
        state.Buffs.Buff[15 * 2 + 1] = 1;
        state.DarkAttackKind = 2;

        zone.Tick(OneGateOpening);

        Assert.Equal(0, state.Buffs.Buff[15 * 2]);
        Assert.Equal(0, state.DarkAttackKind);
    }

    [Fact]
    public void Simulate_Slot17Expiry_ClearsHitRateKind()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[17 * 2] = 60;
        state.Buffs.Buff[17 * 2 + 1] = 1;
        state.HitRateKind = 1;

        zone.Tick(OneGateOpening);

        Assert.Equal(0, state.Buffs.Buff[17 * 2]);
        Assert.Equal(0, state.HitRateKind);
    }

    [Fact]
    public void Simulate_Slot18Expiry_ClearsDodgeRateKind()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[18 * 2] = 60;
        state.Buffs.Buff[18 * 2 + 1] = 1;
        state.DodgeRateKind = 1;

        zone.Tick(OneGateOpening);

        Assert.Equal(0, state.Buffs.Buff[18 * 2]);
        Assert.Equal(0, state.DodgeRateKind);
    }

    [Fact]
    public void Simulate_UnrelatedSlotExpiry_LeavesExclusivityFlagsUntouched()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[10 * 2] = 40;
        state.Buffs.Buff[10 * 2 + 1] = 1;
        state.DarkAttackKind = 2;
        state.HitRateKind = 1;
        state.DodgeRateKind = 1;

        zone.Tick(OneGateOpening);

        Assert.Equal(2, state.DarkAttackKind);
        Assert.Equal(1, state.HitRateKind);
        Assert.Equal(1, state.DodgeRateKind);
    }

    [Fact]
    public void Simulate_AlreadyLapsedSlotWithStaleDisplayValue_ClearsWithoutTouchingExclusivityFlag()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[15 * 2] = 40;
        state.Buffs.Buff[15 * 2 + 1] = 0;
        state.DarkAttackKind = 2;

        zone.Tick(OneGateOpening);

        Assert.Equal(0, state.Buffs.Buff[15 * 2]);
        Assert.Equal(2, state.DarkAttackKind);
    }

    [Fact]
    public void Simulate_AlreadyLapsedSlotWithStaleDisplayValue_MarksSlotAsExpired()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[15 * 2] = 40;
        state.Buffs.Buff[15 * 2 + 1] = 0;

        zone.Tick(OneGateOpening);

        Assert.Equal(2, state.BuffChangeScratch[15]);
    }

    [Fact]
    public void Simulate_MagnitudeAlreadyLapsedWithDurationStillSet_LeavesSlotUntouched()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[10 * 2] = 0;
        state.Buffs.Buff[10 * 2 + 1] = 5;

        zone.Tick(OneGateOpening);

        Assert.Equal(0, state.Buffs.Buff[10 * 2]);
        Assert.Equal(5, state.Buffs.Buff[10 * 2 + 1]);
    }

    [Fact]
    public void Simulate_DarkAttackPotionDebuffSlot_DecrementsAndClearsLikeAnyOtherSlot()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[16 * 2] = 40;
        state.Buffs.Buff[16 * 2 + 1] = 1;

        zone.Tick(OneGateOpening);

        Assert.Equal(0, state.Buffs.Buff[16 * 2]);
        Assert.Equal(0, state.Buffs.Buff[16 * 2 + 1]);
    }

    [Fact]
    public void Simulate_MidZoneTransfer_IsSkippedEntirely()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[0] = 10;
        state.Buffs.Buff[1] = 1;
        state.IsMovingZone = true;

        zone.Tick(OneGateOpening);

        Assert.Equal(10, state.Buffs.Buff[0]);
        Assert.Equal(1, state.Buffs.Buff[1]);
    }

    [Fact]
    public void Simulate_HitRatePotionBuff_ExpiresAfterExactlySixtyGateOpenings_NeitherHalvedNorDoubled()
    {
        var zone = ZoneTestKit.CreateZone(1,
            simulationSystems: [new AvatarOneSecondGateSystem(), new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));

        var slot = new HotkeySlot(HotkeyBindingKind.Item, 12345, 1);
        var result = HotkeyItemConsumptionResolver.Resolve(
            0, 0, slot,
            false, false, true,
            true, HotkeyItemConsumptionResolver.ConsumableItemCategory, 14, 0,
            100, 100, 100, 100,
            true, 0,
            false, 0, false, 0);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(60, write.DurationTicks);

        state!.Buffs.Buff[write.Slot * 2] = write.Value;
        state.Buffs.Buff[write.Slot * 2 + 1] = write.DurationTicks;

        for (var opening = 0; opening < write.DurationTicks - 1; opening++)
            zone.Tick(OneGateOpening);

        Assert.Equal(write.Value, state.Buffs.Buff[write.Slot * 2]);
        Assert.Equal(1, state.Buffs.Buff[write.Slot * 2 + 1]);

        zone.Tick(OneGateOpening);

        Assert.Equal(0, state.Buffs.Buff[write.Slot * 2]);
        Assert.Equal(0, state.Buffs.Buff[write.Slot * 2 + 1]);
    }
}
