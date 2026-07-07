using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <c>Zone.ApplyCombatCommand</c> end-to-end (mCase 2, Avatar vs. enemy-tribe Avatar): HP mutated on tick,
///     death wired to <see cref="Zone.ApplyDeath" />.
/// </summary>
public class ZoneAttackTests
{
    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats WeakDefender =
        new(1000, 1000, 0, 200, 100, 0, 0, 0, 0, 0, 0);

    private static AttackForProtocol MeleeRequest(int attackerId, int defenderId, int mCase = 2)
    {
        return new AttackForProtocol
        {
            Case = mCase,
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

    private static Zone TwoPlayerZone(out FakeDuplexPipe attackerPipe, out FakeDuplexPipe defenderPipe,
        byte attackerTribe = 0, byte defenderTribe = 1, short mapId = 1)
    {
        var zone = ZoneTestKit.CreateZone(mapId, randomSource: new ScriptedRandomSource(0, 0));
        var (attackerSession, aPipe) = ZoneTestKit.CreateSession(1);
        var (defenderSession, dPipe) = ZoneTestKit.CreateSession(2);
        attackerPipe = aPipe;
        defenderPipe = dPipe;

        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: attackerTribe)));
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, 1, "Defender", tribe: defenderTribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Stats = StrongAttacker;
        defender!.Stats = WeakDefender;
        // Legal, already-acting pose -- a real client's own attack is always preceded by an avatar-action
        // packet that sets this, and CombatResolver.ResolveEnemyTribeAttack now rejects a defender whose
        // ActionSort is still the never-initialized 0 (NoActionYetSort) or 12 (DeathPoseSort). See
        // PlayerRuntimeState.ActionSort's own remarks for why this is set per-fixture rather than defaulted.
        defender.ActionSort = 1;

        // This suite exercises combat resolution, not the attack sub-packet budget/replay guard (that's
        // AttackPacketBudgetTests' own job) -- a real client always sends a legal avatar-action packet first
        // to establish a non-zero ceiling, which these fixtures skip. Uncapped here so a raw CombatCommand
        // posted straight after Enter isn't silently rejected by AttackPacketBudget.TryConsume.
        attacker.AttackSubPacketCeiling = int.MaxValue;

        // past both sides' zone-entry protect window, else every attack below is rejected as Attacker/DefenderProtected
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        return zone;
    }

    [Fact]
    public void EnemyTribeAttack_AppliesDamageToDefender()
    {
        var zone = TwoPlayerZone(out _, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.Life < lifeBefore);
    }

    [Fact]
    public void EnemyTribeAttack_BroadcastsAttackResponseToBothParticipants()
    {
        var zone = TwoPlayerZone(out var attackerPipe, out var defenderPipe);

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(attackerPipe));
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(defenderPipe));
    }

    [Fact]
    public void LethalDamage_KillsDefenderAndSchedulesRevive()
    {
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0, 0));
        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: 0)));
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, 1, "Defender", tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Stats = StrongAttacker;
        defender!.Stats = WeakDefender;
        // Legal, already-acting pose -- a real client's own attack is always preceded by an avatar-action
        // packet that sets this, and CombatResolver.ResolveEnemyTribeAttack now rejects a defender whose
        // ActionSort is still the never-initialized 0 (NoActionYetSort) or 12 (DeathPoseSort). See
        // PlayerRuntimeState.ActionSort's own remarks for why this is set per-fixture rather than defaulted.
        defender.ActionSort = 1;
        defender.Life = 1; // one hit will kill regardless of exact damage roll
        attacker.AttackSubPacketCeiling = int.MaxValue; // see TwoPlayerZone's own remarks

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past the zone-entry protect window

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, defender.Life);
        Assert.True(defender.IsDead);
    }

    [Fact]
    public void SameTribe_NoDamageApplied()
    {
        var zone = TwoPlayerZone(out _, out _, 0, 0);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(lifeBefore, defender.Life);
    }

    /// <summary>
    ///     pvp-flagging-safezone-rules finding (Critical): zone 324 and FFAMAPNUM/335 skip the same-tribe/
    ///     allied-tribe rejection entirely inside <c>AttackPlayer</c>'s non-duel branch
    ///     (<c>Server/ts25zone/S07_MyGame02.cpp:952-958</c>) -- same-tribe damage must land on these two maps even
    ///     though the identical attack is rejected everywhere else, see <see cref="SameTribe_NoDamageApplied" />.
    /// </summary>
    [Theory]
    [InlineData((short)324)]
    [InlineData((short)335)]
    public void SameTribe_OnOpenPvpMap_DamageIsApplied(short mapId)
    {
        Assert.True(ZonePvpZoneCatalog.IsSameTribeAttackExempt(mapId));
        Assert.True(ZonePvpZoneCatalog.AllowsEnemyTribeAttack(mapId)); // zone-wide gate must already pass too

        var zone = TwoPlayerZone(out _, out _, 0, 0, mapId);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.Life < lifeBefore);
    }

    /// <summary>
    ///     Blocker #2 (PvP zone gate) end-to-end: <c>Zone.ApplyCombatCommand</c> must resolve
    ///     <see cref="ZonePvpZoneCatalog.AllowsEnemyTribeAttack" /> for its own <see cref="Zone.MapId" /> and pass
    ///     it through to <see cref="CombatResolver.ResolveEnemyTribeAttack" /> -- zone 39 ("The Abyss",
    ///     <c>Server/Header/S18_MyZoneInfo.cpp:180</c>) is one of the legacy's explicit flag-0 zones, so an
    ///     enemy-tribe attack there must never land, regardless of how strong the attacker is.
    /// </summary>
    [Fact]
    public void PvpDisabledZone_EnemyTribeAttackIsRejected()
    {
        Assert.False(ZonePvpZoneCatalog.AllowsEnemyTribeAttack(39));

        var zone = TwoPlayerZone(out _, out _, mapId: 39);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(lifeBefore, defender.Life);
    }

    /// <summary>
    ///     Same gate, opposite side: zone 146 (<c>Server/Header/S18_MyZoneInfo.cpp:190</c>) is one of the
    ///     legacy's explicit flag-1 ("open PvP") zones, so the same attack that's rejected in
    ///     <see cref="PvpDisabledZone_EnemyTribeAttackIsRejected" /> must land here.
    /// </summary>
    [Fact]
    public void PvpEnabledZone_EnemyTribeAttackIsAllowed()
    {
        Assert.True(ZonePvpZoneCatalog.AllowsEnemyTribeAttack(146));

        var zone = TwoPlayerZone(out _, out _, mapId: 146);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.Life < lifeBefore);
    }

    /// <summary>
    ///     pvp-flagging-safezone-rules finding (Major): zone 2 (one of the nine home-tribe-district sub-zones,
    ///     <c>Server/ts25zone/S07_MyGame02.cpp:960-976</c>) blocks a level-90+ attacker from landing a hit on a
    ///     sub-90 defender, even though the zone-wide gate and the tribe check both already pass.
    /// </summary>
    [Fact]
    public void NewbieProtectionZone_HighLevelAttacker_CannotDamageLowLevelDefender()
    {
        Assert.True(ZonePvpZoneCatalog.IsNewbieProtectionZone(2));

        var zone = TwoPlayerZone(out _, out _, mapId: 2);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Level = 90;
        defender!.Level = 89;
        var lifeBefore = defender.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(lifeBefore, defender.Life);
    }

    /// <summary>
    ///     Same gate, capital-plaza exception: zone 1 is conspicuously absent from the legacy switch's case
    ///     list, so the identical level gap that's rejected in
    ///     <see cref="NewbieProtectionZone_HighLevelAttacker_CannotDamageLowLevelDefender" /> must land here.
    /// </summary>
    [Fact]
    public void CapitalPlazaZone_HighLevelAttacker_CanStillDamageLowLevelDefender()
    {
        Assert.False(ZonePvpZoneCatalog.IsNewbieProtectionZone(1));

        var zone = TwoPlayerZone(out _, out _, mapId: 1);
        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Level = 90;
        defender!.Level = 89;
        var lifeBefore = defender.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.Life < lifeBefore);
    }

    [Theory]
    [InlineData(1)] // duel -- attacker/defender here share no active duel, see ZoneDuelCombatTests for mCase 1 itself
    [InlineData(3)] // PvM -- no monster entities yet
    [InlineData(4)] // MvP
    [InlineData(5)] // stun
    [InlineData(6)] // unstun
    public void UnimplementedCases_AreSilentNoOps(int mCase)
    {
        var zone = TwoPlayerZone(out _, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2, mCase)
        });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(lifeBefore, defender.Life);
    }

    [Fact]
    public void UnknownAttacker_DoesNotThrow()
    {
        var zone = TwoPlayerZone(out _, out _);

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 999, AttackInfo = MeleeRequest(999, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50)); // must not throw
    }
}
