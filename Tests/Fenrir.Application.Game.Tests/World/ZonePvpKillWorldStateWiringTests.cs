using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZonePvpKillWorldStateWiringTests
{

        private const short SymbolBattleZoneId = 2;

        private const short SymbolBattleCpBonusServerId = PvpKillContributionPointBonuses.SymbolBattleServerId;

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

    private static Zone SetUpZone(short mapId, GameServerOptions? options = null,
        WorldStateService? worldState = null, short attackerLevel = 42, short defenderLevel = 42)
    {
        var zone = ZoneTestKit.CreateZone(mapId, options, randomSource: new ScriptedRandomSource(0, 0),
            worldState: worldState);

        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: 0, level: attackerLevel)));

        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: 1, level: defenderLevel)));

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
    public void SymbolBattleZone_GrantsNothing_WhileWorldStateTribeSymbolBattleInactive()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = SetUpZone(SymbolBattleZoneId, worldState: worldState);
        Assert.True(zone.TryGetPlayer(1, out var attackerBefore));
        var experienceBefore = attackerBefore!.Experience;

        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(0, attacker!.ContributionPoints);
        Assert.Equal(experienceBefore, attacker.Experience);
        Assert.Equal(0, attacker.MissionKillOtherTribe);
    }

    [Fact]
    public void SymbolBattleZone_GrantsFullReward_OnceWorldStateStartsTribeSymbolBattle()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.StartTribeSymbolBattle();
        var zone = SetUpZone(SymbolBattleZoneId, worldState: worldState);
        Assert.True(zone.TryGetPlayer(1, out var attackerBefore));
        var experienceBefore = attackerBefore!.Experience;

        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(attacker!.ContributionPoints > 0);
        Assert.True(attacker.Experience > experienceBefore);
        Assert.Equal(1, attacker.MissionKillOtherTribe);
    }

    [Fact]
    public void SymbolBattleCpBonus_AppliesOnlyWhileWorldStateTribeSymbolBattleActive()
    {
        const short level = PvpKillContributionPointBonuses.SymbolBattleMinimumBaseLevel;

        var inactiveWorldState = ZoneTestKit.CreateWorldState();
        var inactiveZone = SetUpZone(SymbolBattleCpBonusServerId, worldState: inactiveWorldState,
            attackerLevel: level, defenderLevel: level);
        KillDefender(inactiveZone);
        Assert.True(inactiveZone.TryGetPlayer(1, out var inactiveAttacker));

        var activeWorldState = ZoneTestKit.CreateWorldState();
        activeWorldState.StartTribeSymbolBattle();
        var activeZone = SetUpZone(SymbolBattleCpBonusServerId, worldState: activeWorldState,
            attackerLevel: level, defenderLevel: level);
        KillDefender(activeZone);
        Assert.True(activeZone.TryGetPlayer(1, out var activeAttacker));

        Assert.Equal(PvpKillContributionPointBonuses.SymbolBattleBaseLevelBonus,
            activeAttacker!.ContributionPoints - inactiveAttacker!.ContributionPoints);
    }

    [Fact]
    public void CrossTribeCpAddValue_UsesConfiguredValue_NotTheHardcodedShippedLiteral()
    {
        var options = new GameServerOptions { CrossTribeCpAddValue = 9 };
        var zone = SetUpZone(1, options);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        var expectedCp = PvpKillContributionPointCalculator.ComputeBaseAmount(false, false,
                basePerKillAmount: PvpKillContributionPointBonuses.ComputeGameWideAddValue(9))
            + PvpKillContributionPointBonuses.ComputeConditionalBonuses(1, 0, addedCpTribe: -1,
                attackerBaseLevel: 42, symbolBattleActive: false);
        Assert.Equal(expectedCp, attacker!.ContributionPoints);
        Assert.NotEqual(
            PvpKillContributionPointCalculator.ComputeBaseAmount(false, false,
                basePerKillAmount: PvpKillContributionPointBonuses.ComputeGameWideAddValue(3)),
            attacker.ContributionPoints);
    }

    [Fact]
    public void CrossTribeXpRatio_UsesConfiguredValue_NotTheHardcodedShippedLiteral()
    {
        var options = new GameServerOptions { CrossTribeXpRatio = 5 };
        var zone = SetUpZone(1, options);
        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        var scaledBase = PvpKillExperienceScaling.Scale(PvpKillExperienceBaseTable.Lookup(42), 42, 42);
        var zoneMultiplier = PvpKillExperienceScaling.ResolveZoneMultiplier(false, options.CrossTribeXpRatio);
        var expectedGain = PvpKillExperienceCalculator.ComputeGain(scaledBase, 42, 42, false, false, zoneMultiplier);

        Assert.Equal(expectedGain, attacker!.Experience);
        Assert.NotEqual(
            PvpKillExperienceCalculator.ComputeGain(scaledBase, 42, 42, false, false,
                PvpKillExperienceScaling.ResolveZoneMultiplier(false, 2)),
            attacker.Experience);
    }

    [Fact]
    public void Map195_StillGrantsNothing_RegardlessOfWorldState()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.StartTribeSymbolBattle();
        var zone = SetUpZone(195, worldState: worldState);
        Assert.True(zone.TryGetPlayer(1, out var attackerBefore));
        var experienceBefore = attackerBefore!.Experience;

        KillDefender(zone);

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.Equal(0, attacker!.ContributionPoints);
        Assert.Equal(experienceBefore, attacker.Experience);
        Assert.Equal(0, attacker.MissionKillOtherTribe);
    }
}
