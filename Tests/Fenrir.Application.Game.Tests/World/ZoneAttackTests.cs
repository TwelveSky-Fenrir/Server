using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

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

    /// <summary>
    ///     Legacy-parity regression (S07_MyGame02.cpp:1361-1366): the broadcast attack packet carries TWO
    ///     distinct damage numbers -- <c>mAttackViewDamageValue</c> (the full computed hit, captured BEFORE the
    ///     life-cap clamp, which the client displays as the floating damage figure) and
    ///     <c>mAttackRealDamageValue</c> (the life-capped amount actually lost, captured AFTER the clamp). On a
    ///     killing/overkill blow the two MUST diverge: a defender with 10 HP left struck for a computed 160 must
    ///     still see "160" float over them, not a misleading "10". This proves the whole zone pipeline
    ///     (resolver -&gt; <see cref="AttackOutcome" /> -&gt; wire fields -&gt; broadcast bytes), not just the
    ///     resolver, honors that split -- before the fix both fields were collapsed onto the capped value.
    /// </summary>
    [Fact]
    public void KillingBlow_BroadcastCarriesUncappedViewDamage_ButCappedRealDamage()
    {
        var zone = TwoPlayerZone(out var attackerPipe, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 10; // (1000-200)/5 = 160 pre-cap far overkills this

        ZoneTestKit.DrainOutbound(attackerPipe); // clear enter/spawn chatter before the measured attack

        var request = MeleeRequest(1, 2);
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = request });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead); // the blow was lethal (real damage capped to the 10 remaining life)

        // The first frame on the attacker's wire is the ZC_PROCESS_ATTACK_RECV broadcast, emitted before any
        // death-pose broadcast that follows it: [1-byte opcode][68-byte AttackForProtocol payload].
        var actual = ZoneTestKit.DrainOutbound(attackerPipe);
        Assert.True(actual.Length >= 1 + AttackForProtocol.WireSize);
        Assert.Equal(AttackResponse.Opcode, actual[0]);
        Assert.True(AttackForProtocol.TryRead(actual.AsSpan(1, AttackForProtocol.WireSize), out var info));

        Assert.Equal(160, info.AttackViewDamageValue); // full computed hit, BEFORE the life cap
        Assert.Equal(10, info.AttackRealDamageValue); // capped to the defender's remaining life
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

    /// <summary>
    ///     Legacy-parity regression (S04_MyWork02.cpp:1770-1785): the mutual PvP-attack-immunity window
    ///     (<c>mTickCountFor01SecondForProtect</c> / <see cref="PlayerRuntimeState.ZoneEntryAtZoneClock" />)
    ///     has a SECOND write site distinct from the one-shot stamp at zone (re)entry (:838) -- every
    ///     accepted Sort-0 ("rest"/stand-up) CZ_AVATAR_ACTION_SEND action re-arms it too, unconditionally
    ///     (not gated on <see cref="PlayerRuntimeState.IsDead" />). A real client always settles into
    ///     exactly this idle pose the instant it stops moving after standing back up post-respawn, so a
    ///     character who has been in the zone for a long session -- long past their original zone-entry
    ///     grace window -- is not left with a stale/expired immunity window immediately after dying and
    ///     respawning. <c>Zone.PlayerLifecycle</c>'s <c>ApplyRestActionProtectionAndHeal</c> (wired from
    ///     <c>Zone.HandleMove</c>) already reproduces this; this test proves it end-to-end through
    ///     <c>Zone.ApplyCombatCommand</c> rather than only at the <see cref="CombatResolver" /> unit level.
    /// </summary>
    [Fact]
    public void RestActionAfterLongSession_RearmsZoneEntryProtectWindow_BlockingTheNextAttack()
    {
        var zone = TwoPlayerZone(out _, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));

        // Simulate a long session: push the clock far past both sides' original zone-entry grace window
        // (already exhausted once by TwoPlayerZone itself) by another half hour of play.
        zone.Tick(TimeSpan.FromMinutes(30));

        // The client's own idle packet the instant it settles after standing up post-respawn -- Type 0,
        // Sort 0 (RestActionSort in Zone.PlayerLifecycle.cs).
        zone.Post(ZoneCommand.Move(2, RestOrMoveAction(0, 100, 100)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Immediately follow with a legal, already-acting pose (Sort 2, "plain movement") so the UNRELATED
        // defender-action-state gate (ActionSort == 0 == CombatResolver's own "no action yet" placeholder,
        // S07_MyGame02.cpp:921-924) doesn't also reject the next attack for a different reason and confound
        // this assertion. ZoneEntryAtZoneClock, once armed by the Sort-0 action above, is untouched by this
        // follow-up move -- only a Sort-0 action re-arms it (Zone.PlayerLifecycle.cs's own RestActionSort
        // gate).
        zone.Post(ZoneCommand.Move(2, RestOrMoveAction(2, 100, 100)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var lifeAfterRest = defender!.Life;

        // Attacking immediately after the fresh Sort-0 action must be rejected: the window was just re-armed.
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(lifeAfterRest, defender.Life);

        // Once the FRESH window itself elapses, the identical attack lands normally -- proving this is a
        // genuine, re-armable ~10s grace period tied to the Sort-0 action's own timestamp, not a permanent
        // block or an artifact of some other gate.
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(defender.Life < lifeAfterRest);
    }

    /// <summary>
    ///     Legacy-parity regression: an mCase-2 skill attack's MANA cost is charged from the caster's INVESTED
    ///     grade (<c>aSkillGradeNum1</c>, recorded from the originating op15 action onto
    ///     <see cref="PlayerRuntimeState.ActionSkillGradeNum1" />) ALONE -- not from the combined num1+num2
    ///     grade carried by <c>AttackActionValue3</c> (which the DAMAGE ratio, factor 7, legitimately needs).
    ///     Réf. C++ : <c>Server/ts25zone/S04_MyWork02.cpp:1640</c> -- the op15 <c>tSkillSort==2</c> mana charge
    ///     passes <c>r-&gt;tAction.aSkillGradeNum1</c> alone to <c>ReturnSkillValue</c> factor 1. Before the
    ///     fix, Fenrir charged the combined grade, over-charging every skill-bonus-item holder.
    /// </summary>
    [Fact]
    public void SkillAttackManaCharge_UsesInvestedGradeOnly_NotCombinedItemBonusGrade()
    {
        // Skill 200: ManaUse interpolates 0 (grade0) -> 100 (grade1) over MaxUpgradePoint 10; no
        // AttackPowerRatio, so the damage stays plain and this test isolates the mana charge alone.
        var row = new SkillRowDto(200, "Test Attack Skill", 0, 0, 0, 0, 0, 1, 10, 1, 0);
        var g0 = new SkillGradeRowDto(200, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0);
        var g1 = new SkillGradeRowDto(200, 1, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0);
        var skill = new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [g0, g1]);
        var worldData = ZoneTestKit.EmptyWorldData(
            skillsById: new Dictionary<int, SkillDefinition> { [200] = skill }.ToFrozenDictionary());

        var zone = ZoneTestKit.CreateZone(1, worldData: worldData, randomSource: new ScriptedRandomSource(0, 0));
        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: 0)));
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, 1, "Defender", tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Stats = StrongAttacker;
        defender!.Stats = WeakDefender;
        defender.ActionSort = 1; // legal already-acting pose (see TwoPlayerZone's own remarks)
        attacker.AttackSubPacketCeiling = int.MaxValue;
        // The originating op15 skill action recorded invested grade num1=5 and item-bonus grade num2=5.
        attacker.ActionSkillGradeNum1 = 5;
        attacker.ActionSkillGradeNum2 = 5;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1)); // past the zone-entry protect window

        var manaBefore = attacker.Mana; // 300 (EnterData default)

        var skillAttack = new AttackForProtocol
        {
            Case = 2,
            ServerIndex1 = 1,
            UniqueNumber1 = 1u,
            ServerIndex2 = 2,
            UniqueNumber2 = 2u,
            SenderLocation = [100, 0, 100],
            AttackActionValue1 = 2, // skill attack
            AttackActionValue2 = 200, // skill id
            AttackActionValue3 = 10, // combined grade (num1+num2) -- what the damage ratio uses, NOT the mana
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = skillAttack });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Mana charged from invested num1=5 alone: ReturnSkillValue(sPoint=5) = 0 + (100-0)*5/10 = 50. The
        // pre-fix bug used AttackActionValue3=10 -> ReturnSkillValue(10) = 100.
        Assert.Equal(manaBefore - 50, attacker.Mana);
    }

    private static ActionInfo RestOrMoveAction(int sort, float x, float z)
    {
        return new ActionInfo
        {
            Type = 0,
            Sort = sort,
            Frame = 0,
            Location = [x, 0f, z],
            TargetLocation = [x, 0f, z],
            Front = 0f,
            TargetFront = 0f,
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
