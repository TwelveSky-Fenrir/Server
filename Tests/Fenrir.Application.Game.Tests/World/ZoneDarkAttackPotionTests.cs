using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneDarkAttackPotionTests
{
    private const int AttackerBuffSlot = 15;
    private const int DefenderEffectSlot = 16;

    private static readonly EffectiveStats StrongAttacker =
        new(1000, 1000, 1000, 0, 100, 0, 0, 0, 0, 0, 0);

    private static readonly EffectiveStats WeakDefender =
        new(1000, 1000, 0, 200, 100, 0, 0, 0, 0, 0, 0);

    private static readonly ActionInfo IdlePose = new()
    {
        Type = 0,
        Sort = 0,
        Frame = 0,
        Location = [0, 0, 0],
        TargetLocation = [0, 0, 0],
        Front = 0,
        TargetFront = 0,
        PetLocation = [0, 0, 0],
        PetTargetLocation = [0, 0, 0],
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

    private static (Zone Zone, PlayerRuntimeState Attacker, PlayerRuntimeState Defender) TwoPlayerZone()
    {
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0));
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

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        return (zone, attacker, defender);
    }

    [Fact]
    public void SuccessfulHit_AttackerBuffActiveAndRollSucceeds_DebuffsDefender()
    {
        var (zone, attacker, defender) = TwoPlayerZone();
        attacker.Buffs.Buff[AttackerBuffSlot * 2] = 100;
        attacker.Buffs.Buff[AttackerBuffSlot * 2 + 1] = 10;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(defender.IsUnderDarkAttackPotionDebuff);
        Assert.False(defender.CanUseConsumables);
        Assert.True(defender.DarkAttackDebuffActivatedAtUtc > DateTime.UtcNow.AddSeconds(-5));
        Assert.Equal(0, defender.Buffs.Buff[DefenderEffectSlot * 2]);
    }

    [Fact]
    public void SuccessfulHit_AttackerBuffActiveAndRollSucceeds_GeneralAvatarBroadcastReflectsTheFlagNotTheBuffTable()
    {
        var (zone, attacker, defender) = TwoPlayerZone();
        attacker.Buffs.Buff[AttackerBuffSlot * 2] = 100;
        attacker.Buffs.Buff[AttackerBuffSlot * 2 + 1] = 10;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var snapshot = zone.BuildAvatarActionRecv(defender, IdlePose);
        Assert.Equal(1, snapshot.Data.EffectValueForView[DefenderEffectSlot]);
    }

    [Fact]
    public void SuccessfulHit_AttackerBuffInactive_LeavesDefenderUnaffected()
    {
        var (zone, attacker, defender) = TwoPlayerZone();
        attacker.Buffs.Buff[AttackerBuffSlot * 2] = 100;
        attacker.Buffs.Buff[AttackerBuffSlot * 2 + 1] = 0;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(defender.IsUnderDarkAttackPotionDebuff);
        Assert.True(defender.CanUseConsumables);
    }

    [Fact]
    public void SuccessfulHit_DefenderAlreadyAffected_DoesNotRestampOrReRoll()
    {
        var (zone, attacker, defender) = TwoPlayerZone();
        attacker.Buffs.Buff[AttackerBuffSlot * 2] = 100;
        attacker.Buffs.Buff[AttackerBuffSlot * 2 + 1] = 10;
        defender.IsUnderDarkAttackPotionDebuff = true;
        var priorActivation = DateTime.UtcNow.AddSeconds(-1);
        defender.DarkAttackDebuffActivatedAtUtc = priorActivation;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(priorActivation, defender.DarkAttackDebuffActivatedAtUtc);
    }

    [Fact]
    public void ExpirySystem_ThresholdReached_RestoresConsumablesAndClearsEffectFlag()
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new DarkAttackPotionDebuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.IsUnderDarkAttackPotionDebuff = true;
        state.CanUseConsumables = false;
        state.DarkAttackDebuffActivatedAtUtc =
            DateTime.UtcNow - SimulationClock.DarkAttackPotionDebuffDuration - TimeSpan.FromMilliseconds(1);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(state.IsUnderDarkAttackPotionDebuff);
        Assert.True(state.CanUseConsumables);
    }

    [Fact]
    public void ExpirySystem_MidZoneTransfer_IsSkippedEntirely()
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new DarkAttackPotionDebuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.IsUnderDarkAttackPotionDebuff = true;
        state.CanUseConsumables = false;
        state.IsMovingZone = true;
        state.DarkAttackDebuffActivatedAtUtc =
            DateTime.UtcNow - SimulationClock.DarkAttackPotionDebuffDuration - TimeSpan.FromMilliseconds(1);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsUnderDarkAttackPotionDebuff);
        Assert.False(state.CanUseConsumables);
    }

    [Fact]
    public void ZoneTransfer_DoesNotCarryTheDebuffFlagAcrossTheHandoff()
    {
        var source = ZoneTestKit.CreateZone(2);
        var target = ZoneTestKit.CreateZone(3);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 2)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(source.TryGetPlayer(10, out var beforeHandoff));
        beforeHandoff!.IsUnderDarkAttackPotionDebuff = true;
        beforeHandoff.CanUseConsumables = false;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var arrived));
        Assert.False(arrived!.IsUnderDarkAttackPotionDebuff);
    }

    [Fact]
    public void ExpirySystem_BelowThreshold_KeepsDebuffActive()
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new DarkAttackPotionDebuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.IsUnderDarkAttackPotionDebuff = true;
        state.CanUseConsumables = false;
        state.DarkAttackDebuffActivatedAtUtc = DateTime.UtcNow;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.IsUnderDarkAttackPotionDebuff);
        Assert.False(state.CanUseConsumables);
    }

    [Fact]
    public void DuelHit_AttackerBuffActiveAndRollSucceeds_DoesNotDebuffDefender()
    {
        var duels = new DuelRegistry();
        var zone = ZoneTestKit.CreateZone(1, randomSource: new ScriptedRandomSource(0), duelRegistry: duels);
        var (attackerSession, _) = ZoneTestKit.CreateSession(1);
        var (defenderSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(attackerSession, 1, "Attacker", tribe: 0)));
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(defenderSession, 1, "Defender", tribe: 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var attacker));
        Assert.True(zone.TryGetPlayer(2, out var defender));
        attacker!.Stats = StrongAttacker;
        defender!.Stats = WeakDefender;
        defender.ActionSort = 2;
        attacker.AttackSubPacketCeiling = int.MaxValue;
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(1, 2, false));
        Assert.True(duels.TryAnswer(2, true, out _));
        Assert.True(duels.TryStart(1, out _));

        attacker.Buffs.Buff[AttackerBuffSlot * 2] = 100;
        attacker.Buffs.Buff[AttackerBuffSlot * 2 + 1] = 10;

        zone.PostCombatCommand(new CombatCommand { AttackerCharacterId = 1, AttackInfo = MeleeRequest(1, 2, 1) });
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(defender.IsUnderDarkAttackPotionDebuff);
        Assert.True(defender.CanUseConsumables);
    }
}
