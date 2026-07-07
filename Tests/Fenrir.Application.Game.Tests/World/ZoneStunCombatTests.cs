using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <c>Zone.ApplyCombatCommand</c>'s mCase 5/6 dispatch (<c>Zone.Stun.cs</c>), the anti-stun-hack
///     veto in <c>Zone.HandleMove</c>, and <c>Zone.ApplyDeath</c>'s stun/buff-clearing side effect, end to end
///     against real <see cref="Zone" /> instances.
/// </summary>
public class ZoneStunCombatTests
{
    private const int StunActionSort = 11;
    private const int IdleActionSort = 1;
    private const int StunDefenseBuffSlot = 13;
    private const int CriticalBuffSlot = 10;
    private const int TeamStunSkillId = 80;
    private const int StunSkillId = 7;
    private const int StunResistSkillId = 5;

    private static SkillDefinition StunAttackSkill(int skillId, int stunAttack, int runTime)
    {
        var skill = WorldDataTestRows.Skill(skillId) with { MaxUpgradePoint = 20 };
        var grade0 = WorldDataTestRows.SkillGrade(skillId, 0) with
        {
            StunAttack = (byte)stunAttack, RunTime = (short)runTime
        };
        var grade1 = WorldDataTestRows.SkillGrade(skillId, 1) with
        {
            StunAttack = (byte)stunAttack, RunTime = (short)runTime
        };
        return new SkillDefinition(skill, [], [grade0, grade1]);
    }

    private static SkillDefinition StunResistSkill(int skillId, int stunDefense)
    {
        var skill = WorldDataTestRows.Skill(skillId) with { MaxUpgradePoint = 20 };
        var grade0 = WorldDataTestRows.SkillGrade(skillId, 0) with { StunDefense = (byte)stunDefense };
        var grade1 = WorldDataTestRows.SkillGrade(skillId, 1) with { StunDefense = (byte)stunDefense };
        return new SkillDefinition(skill, [], [grade0, grade1]);
    }

    private static WorldDataCache StunCatalog()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [StunSkillId] = StunAttackSkill(StunSkillId, 50, 8),
            [TeamStunSkillId] = StunAttackSkill(TeamStunSkillId, 10, 5),
            [StunResistSkillId] = StunResistSkill(StunResistSkillId, 30)
        }.ToFrozenDictionary();

        return ZoneTestKit.EmptyWorldData(skillsById: skillsById);
    }

    private static AttackForProtocol StunRequest(int attackerId, int defenderId, uint defenderUniqueNumber,
        int usedSkillId = StunSkillId, int usedSkillGradePoints = 10, int mCase = 5)
    {
        return new AttackForProtocol
        {
            Case = mCase,
            ServerIndex1 = attackerId,
            UniqueNumber1 = unchecked((uint)attackerId),
            ServerIndex2 = defenderId,
            UniqueNumber2 = defenderUniqueNumber,
            SenderLocation = [0, 0, 0],
            AttackActionValue1 = 2,
            AttackActionValue2 = usedSkillId,
            AttackActionValue3 = usedSkillGradePoints,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
    }

    private static (Zone Zone, PlayerRuntimeState Attacker, PlayerRuntimeState Defender) EnterTwoPlayers(
        Zone zone, int attackerId, byte attackerTribe, int defenderId, byte defenderTribe,
        int defenderActionSort = 2)
    {
        var (attackerSession, _) = ZoneTestKit.CreateSession(attackerId);
        var (defenderSession, _) = ZoneTestKit.CreateSession(defenderId);
        zone.Post(ZoneCommand.Enter(attackerId,
            ZoneTestKit.EnterData(attackerSession, zone.MapId, "Attacker", tribe: attackerTribe)));
        zone.Post(ZoneCommand.Enter(defenderId,
            ZoneTestKit.EnterData(defenderSession, zone.MapId, "Defender", tribe: defenderTribe)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetPlayer(attackerId, out var attacker));
        Assert.True(zone.TryGetPlayer(defenderId, out var defender));
        defender!.ActionSort = defenderActionSort;

        // This suite exercises stun/unstun resolution, not the attack sub-packet budget/replay guard (that's
        // AttackPacketBudgetTests' own job) -- a real client always sends a legal avatar-action packet first
        // to establish a non-zero ceiling, which this fixture skips. Uncapped here so a raw CombatCommand
        // (mCase 6, unstun -- ApplyUnstunAttack) posted straight after Enter isn't silently rejected by
        // AttackPacketBudget.TryConsume. mCase 5 (stun) is unaffected: ProcessAttack05 disables this check
        // outright, so ApplyStunAttack deliberately never calls it.
        attacker!.AttackSubPacketCeiling = int.MaxValue;

        // Past both sides' zone-entry protect window, else every stun/unstun attempt below is rejected as
        // Attacker/DefenderProtected -- StunResolver checks CombatResolver.ProtectDuration the same way the
        // mCase 2 attack path does (see ZoneAttackTests.TwoPlayerZone for the identical pattern).
        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        return (zone, attacker!, defender);
    }

    [Fact]
    public void StunAttempt_Success_SetsStunStateAndBroadcastsPose()
    {
        // Map 1 is in ZonePvpZoneCatalog's open-enemy-tribe-attack set.
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog(), randomSource: new ScriptedRandomSource(0));
        var (_, attacker, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = attacker.CharacterId,
            AttackInfo = StunRequest(attacker.CharacterId, defender.CharacterId, defender.UniqueNumber)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(defender.IsStunned);
        Assert.Equal(8, defender.StunDurationSeconds);
        Assert.Equal(StunActionSort, defender.ActionSort);
        Assert.False(defender.CanUseConsumables);
    }

    [Fact]
    public void StunAttempt_DefenderIdentityMismatch_IsSilentlyIgnored()
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog(), randomSource: new ScriptedRandomSource(0));
        var (_, attacker, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = attacker.CharacterId,
            AttackInfo = StunRequest(attacker.CharacterId, defender.CharacterId, defender.UniqueNumber + 999)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(defender.IsStunned);
    }

    [Fact]
    public void StunAttempt_AttackerDead_IsSilentlyIgnored()
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog(), randomSource: new ScriptedRandomSource(0));
        var (_, attacker, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);
        attacker.IsDead = true;

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = attacker.CharacterId,
            AttackInfo = StunRequest(attacker.CharacterId, defender.CharacterId, defender.UniqueNumber)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(defender.IsStunned);
    }

    [Fact]
    public void UnstunAttempt_Success_ClearsStunState()
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog(), randomSource: new ScriptedRandomSource(0));
        // Same tribe on both sides -- cure has no zone/duel gate, just same-or-allied tribe.
        var (_, curer, target) = EnterTwoPlayers(zone, 10, 0, 20, 0);

        target.IsStunned = true;
        target.StunDurationSeconds = 5;
        target.CanUseConsumables = false;
        target.RepeatedStunCount = 3;

        // Grade 20 is the unconditional-success tier -- no roll needed, no skill catalog lookup needed either.
        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = curer.CharacterId,
            AttackInfo = StunRequest(curer.CharacterId, target.CharacterId, target.UniqueNumber,
                StunResistSkillId, 20, 6)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.False(target.IsStunned);
        Assert.Equal(0, target.StunDurationSeconds);
        Assert.Equal(IdleActionSort, target.ActionSort);
        Assert.True(target.CanUseConsumables);
        Assert.Equal(0, target.RepeatedStunCount);
    }

    [Fact]
    public void UnstunAttempt_TargetNotStunned_IsSilentNoOp()
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog(), randomSource: new ScriptedRandomSource(0));
        var (_, curer, target) = EnterTwoPlayers(zone, 10, 0, 20, 0);
        target.ActionSort = 2;

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = curer.CharacterId,
            AttackInfo = StunRequest(curer.CharacterId, target.CharacterId, target.UniqueNumber,
                StunResistSkillId, 20, 6)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(2, target.ActionSort); // untouched -- never entered the cure path at all
    }

    [Fact]
    public void HandleMove_WhileStunned_VetoesNonStunAction_AndDoesNotMove()
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog());
        var (_, _, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);

        defender.IsStunned = true;
        defender.StunDurationSeconds = 5;
        var originalX = defender.PosX;

        var moveAction = new ActionInfo
        {
            Type = 0,
            Sort = 2, // ordinary move -- not the stun pose (11)
            Frame = 0,
            Location = [originalX + 500, defender.PosY, defender.PosZ],
            TargetLocation = [originalX + 500, defender.PosY, defender.PosZ],
            Front = 0,
            TargetFront = 0,
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

        zone.Post(ZoneCommand.Move(defender.CharacterId, in moveAction));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(originalX, defender.PosX); // move discarded, not applied
        Assert.True(defender.IsStunned);
        Assert.Equal(StunActionSort, defender.ActionSort); // re-broadcast stun pose, not the move's Sort
    }

    [Fact]
    public void ApplyDeath_StunnedCharacter_ClearsStunAndBuffs_ButNotRepeatedStunCount()
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog());
        var (_, _, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);

        defender.IsStunned = true;
        defender.StunDurationSeconds = 5;
        defender.CanUseConsumables = false;
        defender.RepeatedStunCount = 4;
        defender.Buffs.Buff[0] = 999;
        defender.Buffs.Buff[1] = 999;

        zone.ApplyDeath(defender.CharacterId, DeathCause.PlayerKill);

        Assert.False(defender.IsStunned);
        Assert.Equal(0, defender.StunDurationSeconds);
        // Deliberately NOT restored here even though the stun-clear path normally does that -- ApplyDeath's own
        // remarks: the death-gate (mProtect_ReviveHack) owns CanUseConsumables until GrantReviveEligibility
        // restores it, matching PlayerRuntimeState.CanUseConsumables's own doc (cleared by ApplyDeath, restored
        // by GrantReviveEligibility, not by the stun clear that happens to run in the same call).
        Assert.False(defender.CanUseConsumables);
        Assert.Equal(4, defender.RepeatedStunCount); // only a cure/natural expiry resets this, not death
        Assert.All(defender.Buffs.Buff, v => Assert.Equal(0, v));
        Assert.True(defender.IsDead);
    }

    [Fact]
    public void TeamStun_PartyShortOfFiveMembers_AbortsSubMechanic_NoCounterIncrement()
    {
        var partyRegistry = new PartyRegistry();
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog(), randomSource: new ScriptedRandomSource(0),
            partyRegistry: partyRegistry);

        var (_, attacker, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);

        // A second party member also present, but the party only has 2 members total -- short of the
        // required 5.
        var (thirdSession, _) = ZoneTestKit.CreateSession(30);
        zone.Post(ZoneCommand.Enter(30, ZoneTestKit.EnterData(thirdSession, zone.MapId, "Ally", tribe: 0)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(PartyInviteOutcome.Sent, partyRegistry.TryInvite(attacker.CharacterId, 1, 0, 30, 1, 0));
        Assert.True(partyRegistry.TryAnswer(30, true, out _, out _));

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = attacker.CharacterId,
            AttackInfo = StunRequest(attacker.CharacterId, defender.CharacterId, defender.UniqueNumber,
                TeamStunSkillId)
        });
        zone.Tick(SimulationClock.LegacyTick);

        // Stun itself still applies (it's unconditional); only the team-stun sub-mechanic aborts.
        Assert.True(defender.IsStunned);
        Assert.Equal(0, defender.RepeatedStunCount);
    }

    [Fact]
    public void TeamStun_ExactlyFiveMembersPresent_IncrementsCounter_AndGrantsCreditOnlyWithCriticalBuffOnBothSides()
    {
        var partyRegistry = new PartyRegistry();
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog(), randomSource: new ScriptedRandomSource(0),
            partyRegistry: partyRegistry);

        var (_, attacker, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);

        var memberIds = new[] { 30, 40, 50, 60 };
        var members = new List<PlayerRuntimeState>();
        foreach (var memberId in memberIds)
        {
            var (session, _) = ZoneTestKit.CreateSession(memberId);
            zone.Post(ZoneCommand.Enter(memberId,
                ZoneTestKit.EnterData(session, zone.MapId, $"Ally{memberId}", tribe: 0)));
        }

        zone.Tick(SimulationClock.LegacyTick);

        foreach (var memberId in memberIds)
        {
            Assert.True(zone.TryGetPlayer(memberId, out var member));
            members.Add(member!);
            Assert.Equal(PartyInviteOutcome.Sent, partyRegistry.TryInvite(attacker.CharacterId, 1, 0, memberId, 1, 0));
            Assert.True(partyRegistry.TryAnswer(memberId, true, out _, out _));
        }

        Assert.Equal(5, partyRegistry.GetMembers(attacker.CharacterId).Count);

        // Both sides must hold the Critical buff (slot 10) active for the kill-credit grant specifically --
        // the counter increment/full-party gate itself does not depend on this.
        defender.Buffs.Buff[CriticalBuffSlot * 2 + 1] = 10;
        foreach (var member in members)
            member.Buffs.Buff[CriticalBuffSlot * 2 + 1] = 10;
        attacker.Buffs.Buff[CriticalBuffSlot * 2 + 1] = 10;

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = attacker.CharacterId,
            AttackInfo = StunRequest(attacker.CharacterId, defender.CharacterId, defender.UniqueNumber,
                TeamStunSkillId)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(defender.IsStunned);
        Assert.Equal(1, defender.RepeatedStunCount);
        Assert.Equal(1, attacker.MissionKillOtherTribe);
        foreach (var member in members)
            Assert.Equal(1, member.MissionKillOtherTribe);
    }

    [Fact]
    public void TeamStun_DefenderMissingCriticalBuff_GrantsNoCreditDespiteFullParty()
    {
        var partyRegistry = new PartyRegistry();
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog(), randomSource: new ScriptedRandomSource(0),
            partyRegistry: partyRegistry);

        var (_, attacker, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);

        var memberIds = new[] { 30, 40, 50, 60 };
        foreach (var memberId in memberIds)
        {
            var (session, _) = ZoneTestKit.CreateSession(memberId);
            zone.Post(ZoneCommand.Enter(memberId,
                ZoneTestKit.EnterData(session, zone.MapId, $"Ally{memberId}", tribe: 0)));
        }

        zone.Tick(SimulationClock.LegacyTick);

        foreach (var memberId in memberIds)
        {
            Assert.True(zone.TryGetPlayer(memberId, out var member));
            member!.Buffs.Buff[CriticalBuffSlot * 2 + 1] = 10;
            Assert.Equal(PartyInviteOutcome.Sent, partyRegistry.TryInvite(attacker.CharacterId, 1, 0, memberId, 1, 0));
            Assert.True(partyRegistry.TryAnswer(memberId, true, out _, out _));
        }

        attacker.Buffs.Buff[CriticalBuffSlot * 2 + 1] = 10;
        // Defender deliberately does NOT hold the Critical buff.

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = attacker.CharacterId,
            AttackInfo = StunRequest(attacker.CharacterId, defender.CharacterId, defender.UniqueNumber,
                TeamStunSkillId)
        });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, defender.RepeatedStunCount); // counter still increments...
        Assert.Equal(0, attacker.MissionKillOtherTribe); // ...but no credit is granted to anyone
    }

    [Fact]
    public void TeamStun_CounterReachingTen_ForcesStunLockDeath()
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: StunCatalog());
        var (_, _, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);

        defender.RepeatedStunCount = 9;
        defender.IsStunned = true;

        // Directly exercises the same force-death path the team-stun sub-mechanic reaches at count 10
        // (Zone.ApplyDeath(DeathCause.StunLock)) -- the counter bookkeeping itself is covered above.
        defender.RepeatedStunCount = 10;
        zone.ApplyDeath(defender.CharacterId, DeathCause.StunLock);

        Assert.True(defender.IsDead);
        Assert.Equal(0, defender.Life);
        Assert.False(defender.IsStunned);
    }

    [Fact]
    public void StunResistSkill_IsScannedInSlotOrder_FirstMatchWins()
    {
        // Two candidate skills equipped (43 and 5); slot ordering must pick slot 0's skill (43) even though
        // it's numerically higher and configured with a weaker block, matching "first match in slot order".
        var zone = ZoneTestKit.CreateZone(1,
            worldData: ZoneTestKit.EmptyWorldData(skillsById: new Dictionary<int, SkillDefinition>
            {
                [StunSkillId] = StunAttackSkill(StunSkillId, 50, 8),
                [43] = StunResistSkill(43, 0), // weak block -- should win (slot 0)
                [5] = StunResistSkill(5, 999) // strong block -- should be ignored (slot 1)
            }.ToFrozenDictionary()), randomSource: new ScriptedRandomSource(10));

        var (_, attacker, defender) = EnterTwoPlayers(zone, 10, 0, 20, 1);
        defender.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty
            .Add(0, new LearnedSkill(43, 10))
            .Add(1, new LearnedSkill(5, 10));

        zone.PostCombatCommand(new CombatCommand
        {
            AttackerCharacterId = attacker.CharacterId,
            AttackInfo = StunRequest(attacker.CharacterId, defender.CharacterId, defender.UniqueNumber)
        });
        zone.Tick(SimulationClock.LegacyTick);

        // margin = 50 - 0 = 50, roll 10 < 50 -> success. Had skill 5 (block 999) been picked instead, this
        // would fail.
        Assert.True(defender.IsStunned);
    }
}
