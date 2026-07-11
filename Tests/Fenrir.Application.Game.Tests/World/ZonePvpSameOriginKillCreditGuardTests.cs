using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZonePvpSameOriginKillCreditGuardTests
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

    private static Zone SetUpZone(short mapId, string? attackerSourceIp, string? defenderSourceIp)
    {
        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(0, 0));

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: 0, sourceIp: attackerSourceIp)));

        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: 1, sourceIp: defenderSourceIp)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        attacker!.Stats = StrongAttacker;
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Stats = WeakDefender;
        defender.ActionSort = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        return zone;
    }

    private static void KillDefender(Zone zone)
    {
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
    }

    [Fact]
    public void SameSourceIp_KillStillHappens_ButNoRewardCreditIsGranted()
    {
        var zone = SetUpZone(1, "203.0.113.7", "203.0.113.7");
        Assert.True(zone.TryGetPlayer(1, out var attackerBefore));
        var experienceBefore = attackerBefore!.Experience;

        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(0, attacker!.ContributionPoints);
        Assert.Equal(0, attacker.HeroRankPoints);
        Assert.Equal(0, attacker.MissionKillOtherTribe);
        Assert.Equal(experienceBefore, attacker.Experience);
    }

    [Fact]
    public void DifferentSourceIp_GrantsTheOrdinaryFormulaBasedContributionPoints()
    {
        var zone = SetUpZone(1, "203.0.113.7", "198.51.100.9");
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        var expectedCp = PvpKillContributionPointCalculator.ComputeBaseAmount(false, false,
                basePerKillAmount: PvpKillContributionPointBonuses.ComputeGameWideAddValue(3))
            + PvpKillContributionPointBonuses.ComputeConditionalBonuses(1, 0, addedCpTribe: -1,
                attackerBaseLevel: 42, symbolBattleActive: false);
        Assert.Equal(expectedCp, attacker!.ContributionPoints);
    }

        [Fact]
    public void BothSourceIpsNull_DoesNotFalselyBlockReward()
    {
        var zone = SetUpZone(1, null, null);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(attacker!.ContributionPoints > 0);
    }

    [Fact]
    public void OneSourceIpNull_DoesNotFalselyBlockReward()
    {
        var zone = SetUpZone(1, "203.0.113.7", null);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(attacker!.ContributionPoints > 0);
    }

        [Fact]
    public void SameSourceIp_WithinLevelGap_StillGrantsNothing()
    {
        var zone = SetUpZone(1, "203.0.113.7", "203.0.113.7");
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        Assert.Equal(attacker!.Level, defender!.Level);

        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attackerAfter));
        Assert.Equal(0, attackerAfter!.ContributionPoints);
        Assert.Equal(0, attackerAfter.HeroRankPoints);
    }
}
