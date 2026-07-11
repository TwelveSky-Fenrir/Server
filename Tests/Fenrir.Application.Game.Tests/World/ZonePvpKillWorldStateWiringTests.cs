using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the world-state/config wiring closed by the PvP-kill reward follow-up pass on top of
///     <see cref="ZonePvpKillRewardsTests" />: <see cref="Zone" /> now threads live
///     <see cref="WorldStateService.World" />.<c>TribeSymbolBattle</c> into both
///     <see cref="PvpKillExtendedRewardZones" />' zone-eligibility gate and
///     <see cref="PvpKillContributionPointBonuses.ComputeConditionalBonuses" />' own symbol-battle CP bonus
///     (previously both were hardcoded <see langword="false" />), and <see cref="GameServerOptions.CrossTribeCpAddValue" />/
///     <see cref="GameServerOptions.CrossTribeXpRatio" /> now feed the CP/EXP formulas instead of the hardcoded
///     shipped literals (3/2) they replace -- the defaults are unchanged, only the source of the value moved.
/// </summary>
public class ZonePvpKillWorldStateWiringTests
{
    /// <summary>
    ///     One of the twelve tribe-symbol-battle secondary zones
    ///     (<c>PvpKillExtendedRewardZones.SymbolBattleZoneIds</c>: 2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143) --
    ///     grants the full CP/EXP/drop/daily-mission set only while <see cref="WorldStateService.World" />'s
    ///     <c>TribeSymbolBattle</c> flag is active, nothing at all while it is inactive.
    /// </summary>
    private const short SymbolBattleZoneId = 2;

    /// <summary>Map 38 ("DTM"): CP is force-enabled regardless of the symbol-battle flag, but the flag still gates
    /// the <see cref="PvpKillContributionPointBonuses.SymbolBattleBaseLevelBonus" /> (+10) additive CP term.</summary>
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

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past the zone-entry protect window
        return zone;
    }

    private static void KillDefender(Zone zone)
    {
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1; // one hit will kill regardless of exact damage roll

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);
    }

    [Fact]
    public void SymbolBattleZone_GrantsNothing_WhileWorldStateTribeSymbolBattleInactive()
    {
        var worldState = ZoneTestKit.CreateWorldState(); // TribeSymbolBattle false by default
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
        // Map 38 force-enables CP regardless of the symbol-battle flag (it's the DTM zone), so this isolates the
        // ComputeConditionalBonuses wiring specifically -- both attacker and defender sit at the
        // SymbolBattleMinimumBaseLevel (135) with a zero combined-level gap so the outer 13-level anti-gank gate
        // never interferes.
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
        // Same combined level (42) on both sides, so Scale is a no-op and map 1 isn't a regular-war server, so
        // ResolveZoneMultiplier returns the configured ratio verbatim.
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
        // Zone195TimeEventGate.IsOpen is permanently false (no live legacy trigger ever flips isZone195TimeEvent)
        // -- confirms wiring it through PvpKillRewardZoneRuntimeState.Map195TimeEventActive (replacing the
        // previously-hardcoded false) is not a behavior change: a symbol-battle world-state flip must NOT open
        // this unrelated gate.
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
