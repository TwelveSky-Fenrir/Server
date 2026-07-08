using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <c>Zone.ApplyCombatCommand</c>'s mCase 1 dispatch (<c>Zone.Duel.cs</c>'s <c>ApplyDuelAttack</c>)
///     end to end against real <see cref="Zone" /> instances -- the duel-actual-combat gap: a duel loser must
///     take real, permanent damage and can genuinely die, but must never reach the enemy-tribe PvP-kill reward
///     pipeline (<see cref="Zone.ApplyPvpKillRewards" />'s own remarks; see <see cref="DeathCause.Duel" />).
/// </summary>
public class ZoneDuelCombatTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats WeakDefender =
        new(1000, 1000, 0, 200, 100, 0, 0, 0, 0, 0, 0);

    private static AttackForProtocol DuelRequest(int attackerId, int defenderId)
    {
        return new AttackForProtocol
        {
            Case = 1,
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

    /// <summary>
    ///     Two players entered, past their zone-entry protect window, with an <see cref="ActiveDuel" /> already
    ///     established between them via the real <see cref="DuelRegistry" /> ask/accept/start handshake -- the
    ///     same duel-pairing fact <c>Zone.SharesActiveDuel</c> reads at combat-resolution time.
    /// </summary>
    private static Zone TwoDuelingPlayers(DuelRegistry duels, out FakeDuplexPipe attackerPipe,
        out FakeDuplexPipe defenderPipe, byte attackerTribe = 0, byte defenderTribe = 0, short mapId = 1)
    {
        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(0, 0), duelRegistry: duels);
        var (attackerSession, aPipe) = ZoneTestKit.CreateSession(1);
        var (defenderSession, dPipe) = ZoneTestKit.CreateSession(2);
        attackerPipe = aPipe;
        defenderPipe = dPipe;

        zone.Post(ZoneCommand.Enter(1,
            ZoneTestKit.EnterData(attackerSession, mapId, "Attacker", tribe: attackerTribe)));
        zone.Post(ZoneCommand.Enter(2,
            ZoneTestKit.EnterData(defenderSession, mapId, "Defender", tribe: defenderTribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Stats = StrongAttacker;
        defender!.Stats = WeakDefender;

        // PlayerRuntimeState.ActionSort defaults to 0 ("no action yet"), which is itself one of the two
        // action-states CheckPossibleAttackTarget's avatar-target rule blocks (see
        // AttackRejectReason.DefenderActionStateBlocksTargeting) -- set it to an ordinary, already-acting pose
        // here (same convention as ZoneStunCombatTests.EnterTwoPlayers) so the "successful attack" tests below
        // aren't silently rejected by a fixture default that has nothing to do with what they're testing.
        defender.ActionSort = 2;

        // Same rationale as ZoneAttackTests.TwoPlayerZone -- a raw CombatCommand posted straight after Enter
        // isn't silently rejected by AttackPacketBudget.TryConsume.
        attacker.AttackSubPacketCeiling = int.MaxValue;

        // past both sides' zone-entry protect window
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(1, 2, false));
        Assert.True(duels.TryAnswer(2, true, out _));
        Assert.True(duels.TryStart(1, out _));

        return zone;
    }

    [Fact]
    public void DuelAttack_SharingActiveDuel_AppliesDamage()
    {
        var duels = new DuelRegistry();
        var zone = TwoDuelingPlayers(duels, out _, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = DuelRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.Life < lifeBefore);
    }

    [Fact]
    public void DuelAttack_BroadcastsAttackResponseToBothParticipants()
    {
        var duels = new DuelRegistry();
        var zone = TwoDuelingPlayers(duels, out var attackerPipe, out var defenderPipe);

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = DuelRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(attackerPipe));
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(defenderPipe));
    }

    /// <summary>
    ///     Duels never evaluate <see cref="CombatResolver.ResolveEnemyTribeAttack" />'s own same-tribe/alliance
    ///     gate (S07_MyGame02.cpp:935-943 vs. the contrasting :945-958) -- unlike mCase 2
    ///     (<c>ZoneAttackTests.SameTribe_NoDamageApplied</c>), a same-tribe duel must land.
    /// </summary>
    [Fact]
    public void DuelAttack_SameTribe_DamageIsApplied()
    {
        var duels = new DuelRegistry();
        var zone = TwoDuelingPlayers(duels, out _, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = DuelRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.Life < lifeBefore);
    }

    [Fact]
    public void DuelAttack_NotCurrentlyDueling_IsSilentNoOp()
    {
        // No DuelRegistry handshake performed -- attacker and defender share no active duel.
        var duels = new DuelRegistry();
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0), duelRegistry: duels);
        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: 0)));
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, 1, "Defender", tribe: 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Stats = StrongAttacker;
        defender!.Stats = WeakDefender;
        defender.ActionSort = 2; // an otherwise-attackable pose -- isolates the duel-pairing gate specifically
        attacker.AttackSubPacketCeiling = int.MaxValue;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        var lifeBefore = defender.Life;
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = DuelRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(lifeBefore, defender.Life);
    }

    /// <summary>
    ///     Duel-actual-combat finding (Critical): a duel loser must take real, permanent damage and can
    ///     genuinely die (<c>AVATAR_OBJECT::ProcessForDeath</c>, S07_MyGame04.cpp:1617-1658) -- but, unlike an
    ///     enemy-tribe kill, must never reach <see cref="Zone.ApplyPvpKillRewards" /> (no CP/mission-kill-counter/
    ///     hero-points/EXP for either side).
    /// </summary>
    [Fact]
    public void DuelAttack_LethalHit_KillsDefenderWithDuelCause_GrantsNoPvpKillRewards()
    {
        var duels = new DuelRegistry();
        var zone = TwoDuelingPlayers(duels, out _, out _);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 1; // one hit will kill regardless of exact damage roll

        var attackerCpBefore = attacker!.ContributionPoints;
        var attackerMissionBefore = attacker.MissionKillOtherTribe;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = DuelRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, defender.Life);
        Assert.True(defender.IsDead);
        // The one DeathCause that does NOT arm ReviveHackFlag (Zone.ApplyDeath's own mProtect_ReviveHack gate).
        Assert.False(defender.ReviveHackFlag);

        Assert.Equal(attackerCpBefore, attacker.ContributionPoints);
        Assert.Equal(attackerMissionBefore, attacker.MissionKillOtherTribe);
    }

    [Fact]
    public void DuelAttack_DefenderShopOpen_IsSilentNoOp()
    {
        var duels = new DuelRegistry();
        var zone = TwoDuelingPlayers(duels, out _, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.PshopOpen = true;
        var lifeBefore = defender.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = DuelRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(lifeBefore, defender.Life);
    }

    [Fact]
    public void DuelAttack_DefenderDeathPose_IsSilentNoOp()
    {
        var duels = new DuelRegistry();
        var zone = TwoDuelingPlayers(duels, out _, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.ActionSort = 12; // death pose -- CheckPossibleAttackTarget's avatar-target rule
        var lifeBefore = defender.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = DuelRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(lifeBefore, defender.Life);
    }
}
