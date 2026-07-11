using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

public class PopupEventRewardSystemTests
{
    private static (Zone Zone, PopupEventRewardSystem System) SetUp(short mapId, params PopupEventType[] enabled)
    {
        var flags = new PopupEventState();
        foreach (var type in enabled)
            flags.SetEnabled(type, true);

        var system = new PopupEventRewardSystem(flags);
        var zone = ZoneTestKit.CreateZone(mapId, simulationSystems: [system]);
        return (zone, system);
    }

    private static PlayerRuntimeState Enter(Zone zone, int characterId, byte tribe = 1, short level = 42)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, zone.MapId, name: $"P{characterId}", tribe: tribe, level: level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

    private static void Tick(Zone zone)
    {
        zone.Tick(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void RegularWar_TenKills_FiresRewardOnce_ThenCounterResetsForTheNextTen()
    {
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 145);

        for (var i = 0; i < 9; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);
        Assert.Equal(0, killer.WarPoint);

        system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);
        Assert.Equal(1, killer.WarPoint);

        for (var i = 0; i < 10; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);
        Assert.Equal(2, killer.WarPoint);
    }

    [Fact]
    public void Reward_IsDeferredToSimulate_NotAppliedInlineByTheTrigger()
    {
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 145);

        for (var i = 0; i < 10; i++)
            system.NotifyPvpKill(zone, killer, victim);

        Assert.Equal(0, killer.WarPoint);
        Tick(zone);
        Assert.Equal(1, killer.WarPoint);
    }

    [Fact]
    public void RegularWar_FlagOff_NeverCountsOrRewards()
    {
        var (zone, system) = SetUp(146);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 145);

        for (var i = 0; i < 30; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);

        Assert.Equal(0, killer.WarPoint);
    }

    [Fact]
    public void MapOutsideEveryPopupSet_NeverRewards()
    {
        var (zone, system) = SetUp(999, PopupEventType.RegularWar);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 145);

        for (var i = 0; i < 30; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);

        Assert.Equal(0, killer.WarPoint);
    }

    [Fact]
    public void SelfKill_IsIgnored()
    {
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var actor = Enter(zone, 1, level: 145);

        for (var i = 0; i < 30; i++)
            system.NotifyPvpKill(zone, actor, actor);
        Tick(zone);

        Assert.Equal(0, actor.WarPoint);
    }

    [Fact]
    public void Pvp_AttackerMoreThan13CombinedLevelsAboveVictim_NeverCounts()
    {
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 130);

        for (var i = 0; i < 20; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);

        Assert.Equal(0, killer.WarPoint);
    }

    [Fact]
    public void Pvp_AttackerBelowVictim_IsNotGated()
    {
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var killer = Enter(zone, 1, level: 100);
        var victim = Enter(zone, 2, level: 145);

        for (var i = 0; i < 10; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);

        Assert.Equal(1, killer.WarPoint);
    }

    [Fact]
    public void Yanggok_BelowCapVictim_NeverCounts()
    {
        var (zone, system) = SetUp(38, PopupEventType.YanggokPvp);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 144);

        for (var i = 0; i < 20; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);

        Assert.Equal(0, killer.WarPoint);
    }

    [Fact]
    public void Yanggok_AtCapVictim_FiresAtTen()
    {
        var (zone, system) = SetUp(38, PopupEventType.YanggokPvp);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 145);

        for (var i = 0; i < 9; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);
        Assert.Equal(0, killer.WarPoint);

        system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);
        Assert.Equal(1, killer.WarPoint);
    }

    [Fact]
    public void Invasion_FiresAtFive_WithAtCapVictim()
    {
        var (zone, system) = SetUp(1, PopupEventType.InvasionPvp);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 145);

        for (var i = 0; i < 4; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);
        Assert.Equal(0, killer.WarPoint);

        system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);
        Assert.Equal(1, killer.WarPoint);
    }

    [Fact]
    public void Monster_DropIneligibleKills_NeverCount()
    {
        var (zone, system) = SetUp(145, PopupEventType.MonsterPve);
        var killer = Enter(zone, 1);

        for (var i = 0; i < 400; i++)
            system.NotifyMonsterKill(zone, killer, dropEligible: false);
        Tick(zone);

        Assert.Equal(0, killer.WarPoint);
    }

    [Fact]
    public void Monster_FiresAtFourHundred_ThenResets()
    {
        var (zone, system) = SetUp(145, PopupEventType.MonsterPve);
        var killer = Enter(zone, 1);

        for (var i = 0; i < 399; i++)
            system.NotifyMonsterKill(zone, killer, dropEligible: true);
        Tick(zone);
        Assert.Equal(0, killer.WarPoint);

        system.NotifyMonsterKill(zone, killer, dropEligible: true);
        Tick(zone);
        Assert.Equal(1, killer.WarPoint);

        for (var i = 0; i < 400; i++)
            system.NotifyMonsterKill(zone, killer, dropEligible: true);
        Tick(zone);
        Assert.Equal(2, killer.WarPoint);
    }

    [Fact]
    public void Monster_FlagOff_NeverCounts()
    {
        var (zone, system) = SetUp(145);
        var killer = Enter(zone, 1);

        for (var i = 0; i < 400; i++)
            system.NotifyMonsterKill(zone, killer, dropEligible: true);
        Tick(zone);

        Assert.Equal(0, killer.WarPoint);
    }

    [Fact]
    public void DepartedKiller_RewardIsSafelySkipped()
    {
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var killer = Enter(zone, 1, level: 145);
        var victim = Enter(zone, 2, level: 145);

        for (var i = 0; i < 10; i++)
            system.NotifyPvpKill(zone, killer, victim);

        zone.Post(ZoneCommand.Leave(1));
        Tick(zone);

        Assert.False(zone.TryGetPlayer(1, out _));
        Assert.Equal(0, killer.WarPoint);
    }

    [Fact]
    public void CountersAreIndependentPerCharacter()
    {
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var killerA = Enter(zone, 1, level: 145);
        var killerB = Enter(zone, 2, level: 145);
        var victim = Enter(zone, 3, level: 145);

        for (var i = 0; i < 10; i++)
            system.NotifyPvpKill(zone, killerA, victim);
        for (var i = 0; i < 5; i++)
            system.NotifyPvpKill(zone, killerB, victim);
        Tick(zone);

        Assert.Equal(1, killerA.WarPoint);
        Assert.Equal(0, killerB.WarPoint);
    }
}
