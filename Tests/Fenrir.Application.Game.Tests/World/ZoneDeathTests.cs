using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneDeathTests
{
    [Fact]
    public void ApplyDeath_SetsLifeZeroAndIsDead()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.Equal(0, state!.Life);
        Assert.True(state.IsDead);
    }

    [Fact]
    public void ApplyDeath_UnknownCharacter_IsIgnored()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.ApplyDeath(999);

        Assert.False(zone.TryGetPlayer(999, out _));
    }

    [Fact]
    public void ApplyDeath_AlreadyDead_DoesNotRearmTheDeathGateTimers()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(999, worldState: worldState,
            simulationSystems: [new DeathGateTickSystem(worldState)]);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 999)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);
        zone.Tick(TimeSpan.FromSeconds(3));

        zone.ApplyDeath(10);

        zone.Tick(TimeSpan
            .FromSeconds(3));

        Assert.True(zone.TryGetPlayer(10, out var revived));
        Assert.False(revived!.IsDead);
    }

    [Fact]
    public void Revive_AfterEligibility_ClearsDeathInPlace_SamePositionSameZone()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(999, worldState: worldState,
            simulationSystems: [new DeathGateTickSystem(worldState)]);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 999, posX: 500f, posZ: 500f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);

        zone.Tick(TimeSpan.FromSeconds(4));
        Assert.True(zone.TryGetPlayer(10, out var stillDead));
        Assert.True(stillDead!.IsDead);
        Assert.Equal(500f, stillDead.PosX);
        Assert.Equal(500f, stillDead.PosZ);

        zone.Tick(TimeSpan.FromSeconds(2));

        Assert.True(zone.TryGetPlayer(10, out var revived));
        Assert.False(revived!.IsDead);
        Assert.Equal(1, revived.Life);
        Assert.Equal(999, revived.MapId);
        Assert.Equal(500f, revived.PosX);
        Assert.Equal(500f, revived.PosZ);
    }

    [Fact]
    public void Revive_NeverInitiatesACrossZoneHandoff()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(999, worldState: worldState,
            simulationSystems: [new DeathGateTickSystem(worldState)]);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 999)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);
        zone.Tick(TimeSpan.FromSeconds(6));

        Assert.True(zone.TryGetPlayer(10, out var revived));
        Assert.False(revived!.IsDead);
        Assert.Equal(1, revived.Life);
        Assert.Equal(999, revived.MapId);
    }

    [Fact]
    public void DeadPlayer_HandedOffToAnotherZone_ArrivesStillDead_NotSilentlyRevivedOrLost()
    {
        var source = ZoneTestKit.CreateZone(2);
        var target = ZoneTestKit.CreateZone(3);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        source.ApplyDeath(10);
        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(source.TryGetPlayer(10, out _));

        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var arrived));
        Assert.Equal(3, arrived!.MapId);
        Assert.True(arrived.IsDead);
    }

    [Fact]
    public void DeadPlayer_HandedOffToAnotherZone_CarriesDeathGateStateAcrossTheTransfer()
    {
        var source = ZoneTestKit.CreateZone(2);
        var target = ZoneTestKit.CreateZone(3);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        source.ApplyDeath(10, DeathCause.MonsterKill);
        Assert.True(source.TryGetPlayer(10, out var beforeHandoff));
        beforeHandoff!.TicksSinceDeath = 7;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var arrived));
        Assert.True(arrived!.ReviveHackFlag);
        Assert.False(arrived.CanUseConsumables);
        Assert.Equal(7, arrived.TicksSinceDeath);
    }

    [Fact]
    public void ApplyDeath_MonsterKillCause_ArmsTheAntiAbuseFlag_AndDisablesConsumables()
    {
        var zone = ZoneTestKit.CreateZone(999);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 999)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10, DeathCause.MonsterKill);

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.True(state!.ReviveHackFlag);
        Assert.False(state.CanUseConsumables);
    }

    [Fact]
    public void ApplyDeath_DuelCause_DoesNotArmTheAntiAbuseFlag()
    {
        var zone = ZoneTestKit.CreateZone(999);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 999)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10, DeathCause.Duel);

        Assert.True(zone.TryGetPlayer(10, out var state));
        Assert.False(state!.ReviveHackFlag);
        Assert.False(state.CanUseConsumables);
    }

    [Fact]
    public void BroadcastSuppression_FlaggedRecipientPast30Ticks_ReceivesNothingFromTheKeepAliveRebroadcast()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(999, worldState: worldState,
            simulationSystems: [new DeathGateTickSystem(worldState)]);

        var (deadSession, deadPipe) = ZoneTestKit.CreateSession(1);
        var (moverSession, moverPipe) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(deadSession, 999, posX: 100f, posZ: 100f)));
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(moverSession, 999, posX: 101f, posZ: 101f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(deadPipe);
        ZoneTestKit.DrainOutbound(moverPipe);

        zone.ApplyDeath(10, DeathCause.MonsterKill);
        Assert.True(zone.TryGetPlayer(10, out var deadState));
        deadState!.TicksSinceDeath = SimulationClock.DeathBroadcastSuppressionLegacyTicks;

        zone.Post(ZoneCommand.Move(11, ActionInfo(101f, 0f, 101f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Empty(ZoneTestKit.DrainOutbound(deadPipe));
    }

    [Fact]
    public void BroadcastSuppression_IsDisabled_OnZone124()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(124, worldState: worldState,
            simulationSystems: [new DeathGateTickSystem(worldState)]);

        var (deadSession, deadPipe) = ZoneTestKit.CreateSession(1);
        var (moverSession, moverPipe) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(deadSession, 124, posX: 100f, posZ: 100f)));
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(moverSession, 124, posX: 101f, posZ: 101f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(deadPipe);
        ZoneTestKit.DrainOutbound(moverPipe);

        zone.ApplyDeath(10, DeathCause.MonsterKill);
        Assert.True(zone.TryGetPlayer(10, out var deadState));
        deadState!.TicksSinceDeath = SimulationClock.DeathBroadcastSuppressionLegacyTicks;

        zone.Post(ZoneCommand.Move(11, ActionInfo(101f, 0f, 101f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(deadPipe));
    }

    private static ActionInfo ActionInfo(float x, float y, float z)
    {
        return new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [x, y, z],
            TargetLocation = [x, y, z],
            Front = 0,
            TargetFront = 0,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
    }
}
