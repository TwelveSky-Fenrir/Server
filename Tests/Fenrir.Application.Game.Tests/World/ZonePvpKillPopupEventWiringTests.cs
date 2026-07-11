using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the C16 wiring gap this workstream closes: <c>Zone.ApplyPvpKillRewards</c> forwarding to
///     <see cref="PopupEventRewardSystem.NotifyPvpKill" /> -- previously never called from anywhere, leaving the
///     popup system entirely inert on the PvP side. <c>PopupEventRewardSystemTests</c> already covers
///     <see cref="PopupEventRewardSystem" />'s own counting/threshold/reset logic exhaustively via direct
///     <c>NotifyPvpKill</c> calls; this suite instead proves the call site exists and is placed correctly
///     relative to the C05 anti-farm cooldown, by driving real kills through the full HP-death combat path
///     (reusing <c>ZonePvpKillRewardsTests</c>' own <c>SetUpZone</c>/<c>KillDefender</c> shape).
/// </summary>
public class ZonePvpKillPopupEventWiringTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats WeakDefender =
        new(1000, 1000, 0, 200, 100, 0, 0, 0, 0, 0, 0);

    private static AttackForProtocol MeleeRequest(int attackerId, int defenderId)
    {
        return new AttackForProtocol
        {
            Case = 2,
            ServerIndex1 = attackerId,
            UniqueNumber1 = unchecked((uint)attackerId),
            ServerIndex2 = defenderId,
            UniqueNumber2 = unchecked((uint)defenderId),
            SenderLocation = [100, 0, 100],
            AttackActionValue1 = 1,
            AttackActionValue2 = 0,
            AttackActionValue3 = 0,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
    }

    private static (Zone Zone, PopupEventRewardSystem PopupSystem) SetUpZone(short mapId,
        params PopupEventType[] enabledPopupTypes)
    {
        var flags = new PopupEventState();
        foreach (var type in enabledPopupTypes)
            flags.SetEnabled(type, true);
        var popupSystem = new PopupEventRewardSystem(flags);

        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(0, 0),
            simulationSystems: [popupSystem]);

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: 0, level: 145)));

        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: 1, level: 145)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Stats = WeakDefender;
        defender.ActionSort = 1; // legal, already-acting pose -- see ZoneAttackTests' own TwoPlayerZone remarks
        attacker.AttackSubPacketCeiling = int.MaxValue; // see ZonePvpKillRewardsTests' own remarks

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past the zone-entry protect window
        return (zone, popupSystem);
    }

    /// <summary>
    ///     Kills (or re-kills) the defender: Life is force-set to 1 so one hit always finishes it off, and
    ///     IsDead is force-cleared first so a SECOND/THIRD/... call in the same test can re-kill the same pair
    ///     without driving the full death-gate/eligibility revive state machine -- this suite only needs "the
    ///     defender is alive and killable again", not a faithful revive sequence (that is
    ///     <c>ZoneDeathTests</c>' own job).
    /// </summary>
    private static void KillDefender(Zone zone)
    {
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.IsDead = false;
        defender.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
    }

    [Fact]
    public void PopupCounter_AdvancesOnEveryKill_EvenWhileTheCpRewardIsGatedByTheAntiFarmCooldown()
    {
        // Invasion popup (map 1, PopupEventZoneCatalog's InvasionMaps): threshold 5, cheap to drive to
        // completion in one test, unlike the war/monster counters' 10/400 thresholds.
        var (zone, _) = SetUpZone(1, PopupEventType.InvasionPvp);

        KillDefender(zone);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        var cpAfterFirstKill = attacker!.ContributionPoints;
        Assert.True(cpAfterFirstKill > 0); // ordinary formula-based CP grant on the FIRST kill of this pair

        // Kills 2-5 of the SAME pair, all within the C05 10-minute anti-farm cooldown -- the CP/EXP/hero-point
        // reward path is gated shut for every one of these, but the popup counter must still advance on each,
        // since NotifyPopupEventPvpKill is placed BEFORE the cooldown gate in Zone.ApplyPvpKillRewards.
        for (var i = 0; i < 4; i++)
            KillDefender(zone);

        Assert.Equal(cpAfterFirstKill, attacker.ContributionPoints); // cooldown blocked every repeat CP grant

        zone.Tick(TimeSpan.FromMilliseconds(500)); // one legacy tick -> PopupEventRewardSystem.Simulate delivers
        Assert.Equal(1, attacker.WarPoint); // 5th kill crossed the Invasion threshold
    }

    [Fact]
    public void NoPopupSystemRegistered_KillStillGrantsOrdinaryRewards_AndDoesNotThrow()
    {
        // Default ZoneTestKit.CreateZone has an empty simulationSystems list, so Zone's own
        // _popupEventRewardSystem resolves to null -- NotifyPopupEventPvpKill must degrade to a silent no-op
        // rather than throwing a NullReferenceException from inside the HP-death path.
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0));

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: 0, level: 42)));
        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, 1, "Defender", tribe: 1, level: 42)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Stats = WeakDefender;
        defender.ActionSort = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        KillDefender(zone); // must not throw

        Assert.True(attacker.ContributionPoints > 0); // ordinary reward path is unaffected
    }
}
