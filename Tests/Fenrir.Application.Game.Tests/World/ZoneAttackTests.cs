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
        defender.ActionSort = 1;

        attacker.AttackSubPacketCeiling = int.MaxValue;

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
        defender.ActionSort = 1;
        defender.Life = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, defender.Life);
        Assert.True(defender.IsDead);
    }

        [Fact]
    public void KillingBlow_BroadcastCarriesUncappedViewDamage_ButCappedRealDamage()
    {
        var zone = TwoPlayerZone(out var attackerPipe, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        defender!.Life = 10;

        ZoneTestKit.DrainOutbound(attackerPipe);

        var request = MeleeRequest(1, 2);
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = request });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsDead);

        var actual = ZoneTestKit.DrainOutbound(attackerPipe);
        Assert.True(actual.Length >= 1 + AttackForProtocol.WireSize);
        Assert.Equal(AttackResponse.Opcode, actual[0]);
        Assert.True(AttackForProtocol.TryRead(actual.AsSpan(1, AttackForProtocol.WireSize), out var info));

        Assert.Equal(160, info.AttackViewDamageValue);
        Assert.Equal(10, info.AttackRealDamageValue);
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

        [Theory]
    [InlineData((short)324)]
    [InlineData((short)335)]
    public void SameTribe_OnOpenPvpMap_DamageIsApplied(short mapId)
    {
        Assert.True(ZonePvpZoneCatalog.IsSameTribeAttackExempt(mapId));
        Assert.True(ZonePvpZoneCatalog.AllowsEnemyTribeAttack(mapId));

        var zone = TwoPlayerZone(out _, out _, 0, 0, mapId);
        Assert.True(zone.TryGetPlayer(2, out var defender));
        var lifeBefore = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.Life < lifeBefore);
    }

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
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
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
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

        [Fact]
    public void RestActionAfterLongSession_RearmsZoneEntryProtectWindow_BlockingTheNextAttack()
    {
        var zone = TwoPlayerZone(out _, out _);
        Assert.True(zone.TryGetPlayer(2, out var defender));

        zone.Tick(TimeSpan.FromMinutes(30));

        zone.Post(ZoneCommand.Move(2, RestOrMoveAction(0, 100, 100)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(2, RestOrMoveAction(2, 100, 100)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var lifeAfterRest = defender!.Life;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(lifeAfterRest, defender.Life);

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(defender.Life < lifeAfterRest);
    }

        [Fact]
    public void SkillAttackManaCharge_UsesInvestedGradeOnly_NotCombinedItemBonusGrade()
    {
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
        defender.ActionSort = 1;
        attacker.AttackSubPacketCeiling = int.MaxValue;
        attacker.ActionSkillGradeNum1 = 5;
        attacker.ActionSkillGradeNum2 = 5;

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        var manaBefore = attacker.Mana;

        var skillAttack = new AttackForProtocol
        {
            Case = 2,
            ServerIndex1 = 1,
            UniqueNumber1 = 1u,
            ServerIndex2 = 2,
            UniqueNumber2 = 2u,
            SenderLocation = [100, 0, 100],
            AttackActionValue1 = 2,
            AttackActionValue2 = 200,
            AttackActionValue3 = 10,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = skillAttack });
        zone.Tick(TimeSpan.FromMilliseconds(50));

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
