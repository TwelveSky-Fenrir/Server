using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="PopupEventRewardSystem" />: the three per-character popup counters, per-type gating
///     (map identity + on/off flag + level gates), threshold-crossing reward (the concrete War Point +1 grant,
///     deferred to the owning zone's <see cref="PopupEventRewardSystem.Simulate" /> pass), and counter reset.
///     The item-reward draws are intentionally deferred (unrecoverable item ids -- see the system's own
///     remarks), so reward firing is asserted through <see cref="PlayerRuntimeState.WarPoint" />.
/// </summary>
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
        zone.Tick(TimeSpan.FromMilliseconds(500)); // exactly one legacy tick -> Simulate runs
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
        Assert.Equal(0, killer.WarPoint); // 9 < 10 -- not yet

        system.NotifyPvpKill(zone, killer, victim); // 10th crosses the threshold
        Tick(zone);
        Assert.Equal(1, killer.WarPoint);

        // Counter reset to 0: another full run fires exactly once more.
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

        Assert.Equal(0, killer.WarPoint); // threshold crossed, but reward not delivered until Simulate
        Tick(zone);
        Assert.Equal(1, killer.WarPoint);
    }

    [Fact]
    public void RegularWar_FlagOff_NeverCountsOrRewards()
    {
        var (zone, system) = SetUp(146); // RegularWar flag NOT enabled
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
        var (zone, system) = SetUp(999, PopupEventType.RegularWar); // flag on, but 999 is in no set
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
        var victim = Enter(zone, 2, level: 130); // gap 15 > 13

        for (var i = 0; i < 20; i++)
            system.NotifyPvpKill(zone, killer, victim);
        Tick(zone);

        Assert.Equal(0, killer.WarPoint);
    }

    [Fact]
    public void Pvp_AttackerBelowVictim_IsNotGated()
    {
        // The level gate is one-directional -- an attacker below the victim is never blocked by it.
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var killer = Enter(zone, 1, level: 100);
        var victim = Enter(zone, 2, level: 145); // attacker 45 levels below

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
        var victim = Enter(zone, 2, level: 144); // one short of the 145 cap

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

        system.NotifyPvpKill(zone, killer, victim); // 10th
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

        system.NotifyPvpKill(zone, killer, victim); // 5th -- Invasion threshold is 5, not 10
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
        Assert.Equal(0, killer.WarPoint); // capped at 399 < 400, no reward yet

        system.NotifyMonsterKill(zone, killer, dropEligible: true); // 400th
        Tick(zone);
        Assert.Equal(1, killer.WarPoint);

        // Reset: a fresh run of 400 fires again.
        for (var i = 0; i < 400; i++)
            system.NotifyMonsterKill(zone, killer, dropEligible: true);
        Tick(zone);
        Assert.Equal(2, killer.WarPoint);
    }

    [Fact]
    public void Monster_FlagOff_NeverCounts()
    {
        var (zone, system) = SetUp(145); // MonsterPve flag not enabled
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
            system.NotifyPvpKill(zone, killer, victim); // threshold crossed, reward-due queued

        // Killer leaves before the reward-delivering Simulate runs.
        zone.Post(ZoneCommand.Leave(1));
        Tick(zone); // drains the Leave, then Simulate finds the killer gone and skips -- must not throw

        Assert.False(zone.TryGetPlayer(1, out _));
        Assert.Equal(0, killer.WarPoint); // never granted
    }

    [Fact]
    public void CountersAreIndependentPerCharacter()
    {
        var (zone, system) = SetUp(146, PopupEventType.RegularWar);
        var killerA = Enter(zone, 1, level: 145);
        var killerB = Enter(zone, 2, level: 145);
        var victim = Enter(zone, 3, level: 145);

        // A gets 10 kills (fires), B gets only 5 (no fire).
        for (var i = 0; i < 10; i++)
            system.NotifyPvpKill(zone, killerA, victim);
        for (var i = 0; i < 5; i++)
            system.NotifyPvpKill(zone, killerB, victim);
        Tick(zone);

        Assert.Equal(1, killerA.WarPoint);
        Assert.Equal(0, killerB.WarPoint);
    }
}
