using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>Raw, unvalidated CZ_PROCESS_ATTACK_SEND requests, resolved entirely on the tick thread (zero-SQL combat).</summary>
    private readonly Channel<CombatCommand> _combatInbox = Channel.CreateBounded<CombatCommand>(
        new BoundedChannelOptions(4096) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Process-wide PvP anti-farm gate (C05) -- shared across every <see cref="Zone" /> via
    ///     <see cref="ZoneRegistry" /> in production; defaults to a private instance in tests so each test zone
    ///     starts with a clean cooldown state.
    /// </summary>
    private readonly KillCooldownTracker _killCooldownTracker = killCooldownTracker ?? new KillCooldownTracker();

    /// <summary>
    ///     Null (production default) builds a private one from <paramref name="worldData" /> instead of the
    ///     process-wide singleton <see cref="ZoneRegistry" /> owns, so pre-existing test call sites that
    ///     construct a <see cref="Zone" /> directly keep compiling unchanged.
    /// </summary>
    private readonly QuestCatalog _questCatalog = questCatalog ?? new QuestCatalog(worldData);

    public bool PostCombatCommand(in CombatCommand command)
    {
        return _combatInbox.Writer.TryWrite(command);
    }

    private void DrainCombatCommands()
    {
        while (_combatInbox.Reader.TryRead(out var command))
            try
            {
                ApplyCombatCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} combat command from character {CharacterId} failed", MapId,
                    command.AttackerCharacterId);
            }
    }

    /// <summary>
    ///     Resolves one CZ_PROCESS_ATTACK_SEND request (<c>mCase</c> 1-6 dispatch) entirely on this zone's own
    ///     tick thread. Only <c>mCase</c> 2 (Avatar -&gt; Avatar, enemy tribe) and 3 (Avatar -&gt; Monster) are
    ///     implemented -- see <see cref="CombatResolver" />'s remarks for what else is/isn't modeled and why.
    ///     Every unimplemented case is a silent no-op, not a disconnect: <c>AttackHandler</c> already rejected
    ///     any <c>mCase</c> outside 1-6, so an in-range-but-unwired value is an in-progress feature, not a hostile packet.
    /// </summary>
    private void ApplyCombatCommand(in CombatCommand command)
    {
        // mCase 4 is deliberately not handled here even though a client could send it: the legacy itself only
        // ever reaches ProcessAttack04 from the monster's own AI (S07_MyGame05.cpp:3961) -- see ResolveMonsterAttack.
        if (command.AttackInfo.Case == 3)
        {
            ApplyPvmAttack(command);
            return;
        }

        if (command.AttackInfo.Case != 2)
            return; // mCase 1/4/5/6 -- deliberately unimplemented, see method remarks.

        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;
        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var defenderSnapshot = ToCombatantSnapshot(defenderState);

        var attackSkill = command.AttackInfo.AttackActionValue1 == 2 &&
                          worldData.SkillsById.TryGetValue(command.AttackInfo.AttackActionValue2,
                              out var skillDef)
            ? skillDef
            : null;

        var outcome = CombatResolver.ResolveEnemyTribeAttack(attackerSnapshot, defenderSnapshot,
            command.AttackInfo, _clock, attackSkill, _random,
            ZonePvpZoneCatalog.AllowsEnemyTribeAttack(MapId));

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] = 0; // charge buff slot 8, value half -- single-use

        // "1 + attacker's weapon ItemId" on a hit (client picks the swing animation/effect from this), 0 on a miss.
        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = CombatRecipients(attackerState, defenderState);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        defenderState.Life -= outcome.DamageApplied;
        defenderState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (defenderState.Life <= 0)
        {
            ApplyPvpKillMissionProgress(attackerState, defenderState.CharacterId);
            ApplyDeath(defenderState.CharacterId, DeathCause.PlayerKill);
        }
    }

    /// <summary>
    ///     PvP-kill hook gated by <see cref="KillCooldownTracker" /> (C05): repeatedly farming the same victim
    ///     within <see cref="KillCooldownTracker.DefaultCooldown" /> (10 min) only ever grants this reward once
    ///     per window for that ordered attacker/defender pair. Currently the only reward this unlocks is
    ///     <see cref="PlayerRuntimeState.MissionKillOtherTribe" />, clamped at
    ///     <see cref="KillCooldownTracker.MissionKillOtherTribeCap" /> -- CP/EXP/drop are a separate, not-yet-built
    ///     pipeline (see <c>16_full_opcode_gap_inventory.md</c> §4 C05).
    /// </summary>
    private void ApplyPvpKillMissionProgress(PlayerRuntimeState attackerState, int defenderCharacterId)
    {
        if (!_killCooldownTracker.TryRegisterKill(attackerState.CharacterId, defenderCharacterId, DateTime.UtcNow))
            return;

        attackerState.MissionKillOtherTribe =
            Math.Min(attackerState.MissionKillOtherTribe + 1, KillCooldownTracker.MissionKillOtherTribeCap);
        attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    /// <summary>
    ///     mCase 3, Avatar -&gt; Monster. Reuses <see cref="TryDamageMonster" /> for the HP mutation/death
    ///     handoff so there is exactly one code path that ever decides "this monster just died."
    /// </summary>
    private void ApplyPvmAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;
        if (!_monsters.TryGetValue(command.AttackInfo.ServerIndex2, out var monster))
            return;
        if (monster.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var outcome = MonsterCombatResolver.ResolvePvmAttack(attackerSnapshot, monster, command.AttackInfo, _clock,
            _random);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] =
                0; // charge buff slot 8, value half -- single-use, same convention as mCase 2

        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = new HashSet<int> { attackerState.CharacterId };
        foreach (var id in _grid.Neighbors(attackerState.CurrentCell)) recipients.Add(id);
        foreach (var id in NeighborsOfPosition(monster.PosX, monster.PosZ)) recipients.Add(id);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        TryDamageMonster(monster.ServerIndex, outcome.DamageApplied, attackerState.CharacterId, out _, out _);
    }

    private CombatantSnapshot ToCombatantSnapshot(PlayerRuntimeState state)
    {
        return new CombatantSnapshot(
            state.CharacterId,
            state.Tribe,
            state.IsDead,
            state.Life,
            state.MaxLife,
            state.PosX,
            state.PosY,
            state.PosZ,
            state.ZoneEntryAtZoneClock,
            state.Stats ?? default,
            state.Buffs.Buff[8 * 2]);
    }

    /// <summary>
    ///     Attacker + defender + both their AOI neighbors, deduplicated -- matches the legacy's own "AOI broadcast +
    ///     unicast to the attacker" (contract doc on <c>AttackResponse</c>).
    /// </summary>
    private HashSet<int> CombatRecipients(PlayerRuntimeState attacker, PlayerRuntimeState defender)
    {
        var recipients = new HashSet<int> { attacker.CharacterId, defender.CharacterId };
        foreach (var id in _grid.Neighbors(attacker.CurrentCell)) recipients.Add(id);
        foreach (var id in _grid.Neighbors(defender.CurrentCell)) recipients.Add(id);
        return recipients;
    }

    private void BroadcastAttackResult(IEnumerable<int> recipientCharacterIds, in AttackResponse response)
    {
        foreach (var id in recipientCharacterIds)
            try
            {
                if (_players.TryGetValue(id, out var recipient))
                    recipient.Session.Send(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} attack-result send to character {RecipientId} failed", MapId,
                    id);
            }
    }

    /// <summary>XP hook called once a monster's death resolves its killer.</summary>
    /// <param name="partyMemberIds">
    ///     Killer's full party roster; null/empty for solo. The base gain above is always solo/killer-only; a
    ///     separate flat party bonus (10/20/30/50% of raw XP by 2-5 present members) goes to every present,
    ///     non-dead member in this same zone, including the killer again (verified: the killer's own record
    ///     also matches the source's party-name filter, which does not exclude it).
    /// </param>
    public void GrantMonsterKillExperience(int killerCharacterId, int monsterLevel, int monsterGeneralExperience,
        IReadOnlyList<int>? partyMemberIds = null)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(state.Level);
        var rawGain = ExperienceFormulas.ComputeMonsterKillExperience(fixedLevel, monsterLevel,
            monsterGeneralExperience);
        var finalGain = ExperienceFormulas.ApplyRebirthDivisor(rawGain, state.Level);
        if (finalGain > 0)
            ApplyExperienceGain(state, finalGain);

        if (partyMemberIds is not { Count: > 0 })
            return;

        List<PlayerRuntimeState>? present = null;
        foreach (var memberId in partyMemberIds)
            if (_players.TryGetValue(memberId, out var member) && !member.IsDead)
                (present ??= []).Add(member);

        if (present is not { Count: >= 2 })
            return;

        var bonus = ExperienceFormulas.ComputePartyBonusExperience(present.Count, monsterGeneralExperience);
        if (bonus <= 0)
            return;

        foreach (var member in present)
            ApplyExperienceGain(member, bonus);

        // Local, not a new Zone method: mirrors MyUtil::ProcessForExperience's own per-recipient level-up loop
        // (S07_MyGame05.cpp:3916 calls it once per present party member too, not just the killer).
        void ApplyExperienceGain(PlayerRuntimeState target, int gain)
        {
            var previousExperience = target.Experience;
            target.Experience += gain;
            target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

            var levelUp = LevelProgressionCalculator.ResolveLevelUp(previousExperience, gain, worldData.LevelsByLevel);
            if (!levelUp.LeveledUp)
                return;

            target.Level = levelUp.NewLevel;
            target.StatPoints += levelUp.StatPointsGranted;
            target.SkillPoints += levelUp.SkillPointsGranted;

            var equipmentContainer = target.Inventory.GetContainer(ContainerMatrix.Equipment);
            var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
                ? petStack.ItemId
                : 0;
            var petContribution = PetGrowthCalculator.Compute(petItemId, target.PetGrowth, target.PetActivity,
                worldData.ItemsById);
            var attributes = new CharacterBaseAttributes(target.StatVit, target.StatStr, target.StatInt,
                target.StatDex, target.Level, target.Tribe, target.Title, target.Halo, target.RebirthCount);
            var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, target.Buffs,
                petContribution);

            target.Stats = stats;
            target.MaxLife = stats.MaxLife;
            target.MaxMana = stats.MaxMana;

            // SetBasicAbilityFromEquip's aMaxLifeValue/aMaxManaValue cache-write above happens unconditionally;
            // only the actual heal is gated on the character being alive (S07_MyGame03.cpp:285-289).
            if (target.Life > 0)
            {
                target.Life = stats.MaxLife;
                target.Mana = stats.MaxMana;
            }

            target.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
        }
    }

    /// <summary>
    ///     Quest kill-hook alongside <see cref="GrantMonsterKillExperience" />, from the same kill. Increments
    ///     <see cref="PlayerRuntimeState.QuestKillCounter" /> by 1 when the killer's active quest is qSort 1 or
    ///     5 and <paramref name="monsterId" /> matches <see cref="PlayerRuntimeState.QuestTargetPhase" />, but
    ///     only while the counter is still below its target (qSort 1: below Solution2; qSort 5: still 0) --
    ///     this is the clamp, not an unbounded increment. Party propagation is not modeled here.
    /// </summary>
    public void ApplyQuestKillProgress(int killerCharacterId, int monsterId)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        if (state.QuestActiveFlag != 1 || state.QuestTargetPhase != monsterId)
            return;

        switch (state.QuestSort)
        {
            case 1:
                var quest = _questCatalog.TryGet(state.Tribe, state.QuestStepPermanent);
                if (quest is null)
                    return;
                if (state.QuestKillCounter < (quest.Quest.Solution2 ?? 0))
                {
                    state.QuestKillCounter++;
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                }

                break;
            case 5:
                if (state.QuestKillCounter < 1)
                {
                    state.QuestKillCounter++;
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                }

                break;
        }
    }
}
