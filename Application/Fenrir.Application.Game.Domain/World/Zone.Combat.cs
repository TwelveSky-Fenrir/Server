using System.Buffers;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int CombinedLevelGapCap = 13;

    private const int WarPointStatSort = 905;

    private const int BloodPointAvatarChangeInfoSort = 300;

    private const int ExperienceStatSort = 13;

    private const int LevelUpAvatarChangeInfoSort = 1;

    private const int BonusItemAvatarChangeInfoSort = 107;

    private const int QuestKillCreditArchetype1SoloSort = 6;

    private const int QuestKillCreditRelayOrArchetype5Sort = 8;

    private const int CombatInboxCapacity = 4096;

    private const int CombatInboxDrainCapPerTick = CombatInboxCapacity / 2;

    private readonly Channel<CombatCommand> _combatInbox = Channel.CreateBounded<CombatCommand>(
        new BoundedChannelOptions(CombatInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly List<int> _combatNeighborScratch = [];

    private readonly HashSet<int> _combatRecipientScratch = [];

    private readonly KillCooldownTracker _ffaCpOverrideCooldown = new();

    private readonly KillCooldownTracker _killCooldownTracker = killCooldownTracker ?? new KillCooldownTracker();

    private readonly HashSet<int> _pvmAttackRecipientScratch = [];

    private readonly QuestCatalog _questCatalog = questCatalog ?? new QuestCatalog(worldData);

    private readonly KillCooldownTracker _regularWarCpOverrideCooldown = new();

    public bool PostCombatCommand(in CombatCommand command)
    {
        return _combatInbox.Writer.TryWrite(command);
    }

    private void DrainCombatCommands()
    {
        var processed = 0;
        while (processed < CombatInboxDrainCapPerTick && _combatInbox.Reader.TryRead(out var command))
        {
            processed++;
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

        if (processed >= CombatInboxDrainCapPerTick)
            LogDrainCapEngaged(_combatInbox.Reader, "combat", CombatInboxDrainCapPerTick);
    }

    private void ApplySenderLocation(PlayerRuntimeState state, AttackForProtocol attackInfo)
    {
        state.PosX = attackInfo.SenderLocation[0];
        state.PosY = attackInfo.SenderLocation[1];
        state.PosZ = attackInfo.SenderLocation[2];

        var newCell = _grid.CellOf(state.PosX, state.PosZ);
        _grid.Move(state.CharacterId, state.CurrentCell, newCell, state.PosX, state.PosY, state.PosZ);
        state.CurrentCell = newCell;
    }

    private void ApplyCombatCommand(in CombatCommand command)
    {
        if (command.AttackInfo.Case == 1)
        {
            ApplyDuelAttack(command);
            return;
        }

        if (command.AttackInfo.Case == 3)
        {
            ApplyPvmAttack(command);
            return;
        }

        if (command.AttackInfo.Case == 5)
        {
            ApplyStunAttack(command);
            return;
        }

        if (command.AttackInfo.Case == 6)
        {
            ApplyUnstunAttack(command);
            return;
        }

        if (command.AttackInfo.Case != 2)
            return;

        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;

        ApplySenderLocation(attackerState, command.AttackInfo);

        if (regularWarActiveMapTracker?.IsFightClosed(MapId) == true)
            return;

        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;

        if (!AttackPacketBudget.TryConsume(attackerState, command.AttackInfo.AttackActionValue4))
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var defenderSnapshot = ToCombatantSnapshot(defenderState);

        var attackSkill = command.AttackInfo.AttackActionValue1 == 2 &&
                          worldData.SkillsById.TryGetValue(command.AttackInfo.AttackActionValue2,
                              out var skillDef)
            ? skillDef
            : null;

        if (attackSkill is not null &&
            (!attackerState.AttackBudgetEnforced || attackerState.AttackSubPacketsUsed == 1))
        {
            var manaCost = (int)SkillCatalog.ReturnSkillValue(attackSkill, attackerState.ActionSkillGradeNum1,
                SkillValueKind.ManaUse);
            if (manaCost > 0)
            {
                if (attackerState.Mana < manaCost)
                    return;

                attackerState.Mana -= manaCost;
                attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
            }
        }

        var outcome = CombatResolver.ResolveEnemyTribeAttack(attackerSnapshot, defenderSnapshot,
            command.AttackInfo, _clock, attackSkill, _random,
            ZonePvpZoneCatalog.AllowsEnemyTribeAttack(MapId),
            ZonePvpZoneCatalog.IsSameTribeAttackExempt(MapId),
            ZonePvpZoneCatalog.IsNewbieProtectionZone(MapId),
            defenderState.PshopOpen, defenderState.ActionSort,
            worldState?.GetAllyOf(attackerSnapshot.Tribe),
            worldState?.GetTribeFormationAbility(attackerSnapshot.Tribe) ?? FormationCombatResolver.NoFormation,
            worldState?.GetTribeFormationAbility(defenderSnapshot.Tribe) ?? FormationCombatResolver.NoFormation);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] = 0;

        var viewDamage = outcome.ViewDamage;
        var realDamage = outcome.DamageApplied;
        var reflectFired = false;
        if (outcome.Hit)
        {
            if (TryApplyReflectAndDestroyer(attackerState, defenderState, outcome, CrossAvatarAttackKind.EnemyTribe))
            {
                reflectFired = true;
                viewDamage = 0;
                realDamage = 0;
            }
            else
            {
                (viewDamage, realDamage) = ApplyHolyShieldAbsorption(defenderState, outcome);
            }
        }

        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = reflectFired ? 0 : outcome.ElementDamage,
                AttackViewDamageValue = viewDamage,
                AttackRealDamageValue = realDamage
            }
        };

        var recipients = CombatRecipients(attackerState, defenderState);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit || reflectFired)
            return;

        defenderState.Life -= realDamage;
        defenderState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (defenderState.Life <= 0)
        {
            var killType = ClassifyPvpKillType(command.AttackInfo.AttackActionValue2, false);
            ApplyPvpKillRewards(attackerState, defenderState, killType == KillCpType.Stun);
            RecordEnemyKillForFeed(attackerState, defenderState, killType == KillCpType.Stun,
                regularWarActiveMapTracker?.IsBattleInProgress(MapId) == true ||
                MapId == KillFeedZoneCatalog.FfaMapNumber);
            ApplyDeath(defenderState.CharacterId, DeathCause.PlayerKill);
        }
    }

    private void ApplyPvpKillRewards(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState,
        bool isStunTrigger = false)
    {
        if (attackerState.CharacterId == defenderState.CharacterId)
            return;

        if (PvpKillCreditGuard.IsSameOrigin(attackerState.SourceIp, defenderState.SourceIp))
            return;

        var attackerCombinedLevel = attackerState.Level + attackerState.Level2;
        var defenderCombinedLevel = defenderState.Level + defenderState.Level2;
        if (attackerCombinedLevel - defenderCombinedLevel > CombinedLevelGapCap)
            return;

        NotifyPopupEventPvpKill(attackerState, defenderState);

        if (!_killCooldownTracker.TryRegisterKill(attackerState.CharacterId, defenderState.CharacterId,
                DateTime.UtcNow))
            return;

        var zoneRuntime = new PvpKillRewardZoneRuntimeState(
            worldState?.World.TribeSymbolBattle ?? false,
            Zone195TimeEventGate.IsOpen);
        var profile = PvpKillExtendedRewardZones.TryResolve(MapId, isStunTrigger, zoneRuntime)
                      ?? PvpKillRewardZoneCatalog.Resolve(MapId, isStunTrigger);

        if (profile.GrantDailyMissionProgress)
        {
            attackerState.MissionKillOtherTribe =
                Math.Min(attackerState.MissionKillOtherTribe + 1, KillCooldownTracker.MissionKillOtherTribeCap);
            attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (profile.GrantContributionPoints)
            ApplyTowerCpForPvpBonus(attackerState);

        ApplyPvpKillContributionPointFormula(attackerState, defenderState, profile);
        ApplyPvpKillHeroPoints(attackerState, profile, attackerCombinedLevel);
        ApplyPvpKillExperience(attackerState, profile, attackerCombinedLevel, defenderCombinedLevel);
    }

    private void ApplyTowerCpForPvpBonus(PlayerRuntimeState attackerState)
    {
        var bonus = towerWar?.GetTribeBonus(attackerState.Tribe).CpForPvpBonus ?? 0;
        if (bonus > 0)
            GrantContributionPoints(attackerState.CharacterId, bonus);
    }

    private void ApplyPvpKillContributionPointFormula(PlayerRuntimeState attackerState,
        PlayerRuntimeState defenderState, PvpKillZoneRewardProfile profile)
    {
        if (regularWarActiveMapTracker?.IsBattleInProgress(MapId) == true)
        {
            ApplyRegularWarCpOverride(attackerState, defenderState);
            return;
        }

        if (MapId == PvpKillRewardZoneCatalog.FfaMapNumber)
        {
            if (_ffaCpOverrideCooldown.TryRegisterKill(attackerState.CharacterId, defenderState.CharacterId,
                    DateTime.UtcNow, PvpKillContributionPointCalculator.FlatOverrideCooldown))
            {
                var granted = PvpKillContributionPointCalculator.ClampGrant(attackerState.ContributionPoints,
                    PvpKillContributionPointCalculator.FfaOverrideFlatAmount,
                    PvpKillContributionPointCalculator.PlaceholderHardCap);
                GrantContributionPoints(attackerState.CharacterId, granted);
            }

            return;
        }

        if (!profile.GrantContributionPoints)
            return;

        var baseAmount = PvpKillContributionPointCalculator.ComputeBaseAmount(
            false,
            false,
            PvpKillContributionPointBonuses.ComputePerUserAddValue(false),
            0,
            0,
            PvpKillContributionPointBonuses.ComputeGameWideAddValue(options.CrossTribeCpAddValue));
        baseAmount += PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            MapId, attackerState.Tribe,
            -1,
            attackerState.Level,
            worldState?.World.TribeSymbolBattle ?? false);

        var grantedAmount = PvpKillContributionPointCalculator.ClampGrant(attackerState.ContributionPoints,
            baseAmount, PvpKillContributionPointCalculator.PlaceholderHardCap);
        GrantContributionPoints(attackerState.CharacterId, grantedAmount);
    }

    private void ApplyRegularWarCpOverride(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState)
    {
        if (!_regularWarCpOverrideCooldown.TryRegisterKill(attackerState.CharacterId, defenderState.CharacterId,
                DateTime.UtcNow, PvpKillContributionPointCalculator.FlatOverrideCooldown))
            return;

        var granted = PvpKillContributionPointCalculator.ClampGrant(attackerState.ContributionPoints,
            PvpKillContributionPointCalculator.RegularWarOverrideFlatCpAmount,
            PvpKillContributionPointCalculator.PlaceholderHardCap);
        GrantContributionPoints(attackerState.CharacterId, granted);

        GrantWarPoints(attackerState.CharacterId, PvpKillContributionPointCalculator.RegularWarOverrideWarPointAmount);
        GrantBloodPoints(attackerState.CharacterId,
            PvpKillContributionPointCalculator.RegularWarOverrideBloodPointAmount);
    }

    private void ApplyPvpKillHeroPoints(PlayerRuntimeState attackerState, PvpKillZoneRewardProfile profile,
        int attackerCombinedLevel)
    {
        if (profile.HeroPointAmount <= 0)
            return;
        if (attackerCombinedLevel < PvpKillRewardZoneCatalog.HeroPointMinimumCombinedLevel)
            return;

        attackerState.HeroRankPoints += profile.HeroPointAmount;
        _heroRankPointAccumulator.AddPending(attackerState.CharacterId, profile.HeroPointAmount, attackerState.Tribe,
            attackerState.Level);
    }

    private void ApplyPvpKillExperience(PlayerRuntimeState attackerState, PvpKillZoneRewardProfile profile,
        int attackerCombinedLevel, int defenderCombinedLevel)
    {
        if (profile.GrantExperience)
        {
            var scaledBase = PvpKillExperienceScaling.Scale(
                PvpKillExperienceBaseTable.Lookup(defenderCombinedLevel),
                attackerCombinedLevel,
                defenderCombinedLevel);
            var zoneMultiplier = PvpKillExperienceScaling.ResolveZoneMultiplier(
                RegularWarMapCatalog.TryGet(MapId, out _),
                options.CrossTribeXpRatio);
            var gain = PvpKillExperienceCalculator.ComputeGain(
                scaledBase,
                attackerCombinedLevel,
                defenderCombinedLevel,
                false,
                false,
                zoneMultiplier);

            if (gain > 0)
                ApplyCharacterExperienceGain(attackerState, gain);
        }

        var petExpGain = PvpKillPetExperienceCalculator.ComputeGain(attackerCombinedLevel);
        CreditPetGrowthFromPvpKill(attackerState, petExpGain);

        ApplyPvpKillMountExperience(attackerState);
    }

    private void ApplyPvpKillMountExperience(PlayerRuntimeState attackerState)
    {
        if (attackerState.AnimalIndex is < 10 or > 19)
            return;

        var slot = attackerState.AnimalIndex % 10;
        var mountActivity = attackerState.MountActivity[slot];
        var mountExperience = attackerState.MountAccumulatedExp[slot];

        var gain = MountKillExperienceCalculator.ComputeGain(
            true, mountActivity, mountExperience,
            attackerState.AnimalDoubleExp > 0, attackerState.MountExpUp,
            options.MountKillExperiencePerKill);

        if (gain <= 0)
            return;

        attackerState.MountAccumulatedExp = attackerState.MountAccumulatedExp.SetItem(slot,
            MountActivityExpCodec.ClampExp(mountExperience + gain));
        attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    public void GrantContributionPoints(int characterId, int amount)
    {
        if (amount == 0 || !_players.TryGetValue(characterId, out var state))
            return;

        state.ContributionPoints = Math.Max(0, state.ContributionPoints + amount);
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    public void GrantWarPoints(int characterId, int amount)
    {
        if (amount == 0 || !_players.TryGetValue(characterId, out var state))
            return;

        state.WarPoint += amount;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = WarPointStatSort, Value = state.WarPoint, Value2 = 0 });
    }

    public void GrantBloodPoints(int characterId, int amount)
    {
        if (amount == 0 || !_players.TryGetValue(characterId, out var state))
            return;

        state.BloodCoin += amount;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        state.Session.Send(new AvatarStateFlagResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Sort = BloodPointAvatarChangeInfoSort,
            Value01 = state.BloodCoin,
            Value02 = 0,
            Value03 = 0
        });
    }

    private void ApplyPvmAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;

        ApplySenderLocation(attackerState, command.AttackInfo);

        if (!_monsters.TryGetValue(command.AttackInfo.ServerIndex2, out var monster))
            return;
        if (monster.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

        if (monster.AiState is MonsterAiState.Spawning or MonsterAiState.Dead or MonsterAiState.ReturnToSpawn)
            return;

        if (!AttackPacketBudget.TryConsume(attackerState, command.AttackInfo.AttackActionValue4))
            return;

        var towerIndex = TowerZoneIndexTable.GetTowerIndex(MapId);
        var isTowerGuardian = towerIndex >= 0 && monster.ServerIndex == TowerWarState.GuardianServerIndex(towerIndex);

        if (isTowerGuardian && !CanAttackTowerGuardian(attackerState.Tribe, towerIndex))
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var outcome = MonsterCombatResolver.ResolvePvmAttack(attackerSnapshot, monster, command.AttackInfo, _clock,
            _random, attackerState.AttackBudgetEnforced, attackerState.ActionSkillNumber,
            attackerState.ActionSkillGradeNum1 + attackerState.ActionSkillGradeNum2,
            tribeSymbolCombatModifiers?.GetDamageDownPenalty(attackerSnapshot.Tribe) ?? 0f,
            tribeSymbolCombatModifiers?.GetDamageUpBonusIncrementCount(attackerSnapshot.Tribe) ?? 0);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] =
                0;

        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.ViewDamage,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        _pvmAttackRecipientScratch.Clear();
        _pvmAttackRecipientScratch.Add(attackerState.CharacterId);

        _combatNeighborScratch.Clear();
        _grid.Neighbors(_combatNeighborScratch, attackerState.CurrentCell, attackerState.PosX, attackerState.PosY,
            attackerState.PosZ);
        foreach (var id in _combatNeighborScratch)
            _pvmAttackRecipientScratch.Add(id);

        _combatNeighborScratch.Clear();
        _grid.Neighbors(_combatNeighborScratch, _grid.CellOf(monster.PosX, monster.PosZ), monster.PosX,
            monster.PosY, monster.PosZ);
        foreach (var id in _combatNeighborScratch)
            _pvmAttackRecipientScratch.Add(id);

        BroadcastAttackResult(_pvmAttackRecipientScratch, response);

        if (!outcome.Hit)
            return;

        TryDamageMonster(monster.ServerIndex, outcome.DamageApplied, attackerState.CharacterId, out var monsterDied,
            out _);

        if (monsterDied)
            MonsterDeathSequence.BeginCorpseCountdown(monster, attackerState.PosX, attackerState.PosZ,
                outcome.Critical, _random);

        if (!monsterDied)
            TryApplyPvmFlinch(monster, outcome.DamageApplied);

        if (isTowerGuardian)
            ApplyTowerGuardianHitSideEffects(towerIndex, attackerState);
    }

    private bool CanAttackTowerGuardian(byte attackerTribe, int towerIndex)
    {
        var owningTribe = TowerZoneIndexTable.GetOwningTribe(MapId);
        var towerActivelyBuilt = towerWar?.GetPhase(towerIndex) == TowerSiegePhase.Active;
        var allyOfOwningTribe = owningTribe is { } owner ? worldState?.GetAllyOf(owner) : null;

        return TowerFriendlyFireGate.CanAttackGuardian(attackerTribe, owningTribe, towerActivelyBuilt,
            allyOfOwningTribe);
    }

    private void ApplyTowerGuardianHitSideEffects(int towerIndex, PlayerRuntimeState attackerState)
    {
        if (towerWar is null)
            return;

        var isFirstHit = towerWar.RecordGuardianHit(towerIndex, DateTime.UtcNow);
        if (!isFirstHit)
            return;

        var packedState = towerWar.GetPackedState(towerIndex);
        logger.LogInformation(
            "Tower siege started (Center broadcast 754): towerType={TowerType} attackerTribe={AttackerTribe} zoneServerNumber={ZoneServerNumber} attackerName={AttackerName}",
            TowerWarState.DecodeType(packedState), attackerState.Tribe, MapId, attackerState.Name);

        BroadcastTowerStatus();
    }

    private void BroadcastTowerStatus()
    {
        if (towerWar is null)
            return;

        var response = towerWar.BuildStatusSnapshot();

        var total = FrameWriter.FrameSizeOf<TowerStatusResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var player in _players.Values)
                try
                {
                    if (player.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} tower-status broadcast to character {RecipientId} failed",
                        MapId, player.CharacterId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
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
            FfaEqualStatsOverride.Apply(MapId, state.Stats ?? default),
            state.Buffs.Buff[8 * 2],
            state.Level,
            state.IsMovingZone,
            state.Name);
    }

    private HashSet<int> CombatRecipients(PlayerRuntimeState attacker, PlayerRuntimeState defender)
    {
        _combatRecipientScratch.Clear();
        _combatRecipientScratch.Add(attacker.CharacterId);
        _combatRecipientScratch.Add(defender.CharacterId);

        _combatNeighborScratch.Clear();
        _grid.Neighbors(_combatNeighborScratch, attacker.CurrentCell, attacker.PosX, attacker.PosY, attacker.PosZ);
        foreach (var id in _combatNeighborScratch)
            _combatRecipientScratch.Add(id);

        _combatNeighborScratch.Clear();
        _grid.Neighbors(_combatNeighborScratch, defender.CurrentCell, defender.PosX, defender.PosY, defender.PosZ);
        foreach (var id in _combatNeighborScratch)
            _combatRecipientScratch.Add(id);

        return _combatRecipientScratch;
    }

    private void BroadcastAttackResult(IReadOnlyCollection<int> recipientCharacterIds, in AttackResponse response)
    {
        if (recipientCharacterIds.Count == 0)
            return;

        var total = FrameWriter.FrameSizeOf<AttackResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var id in recipientCharacterIds)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} attack-result send to character {RecipientId} failed", MapId,
                        id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public void GrantMonsterKillExperience(int killerCharacterId, int monsterLevel, int monsterGeneralExperience,
        IReadOnlyList<int>? partyMemberIds = null, int monsterPatExperience = 0, int monsterLifeValue = 0)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(state.Level);
        var rawGain = ExperienceFormulas.ComputeMonsterKillExperience(fixedLevel, monsterLevel,
            monsterGeneralExperience);

        var xpBonusRatio = towerWar?.GetTribeBonus(state.Tribe).XpRatio ?? 0f;
        if (xpBonusRatio > 0f && monsterGeneralExperience >= 1)
            rawGain += (int)(monsterGeneralExperience * xpBonusRatio);

        var finalGain = ExperienceFormulas.ApplyRebirthDivisor(rawGain, state.Level);

        if (MonsterKillExperienceGate.ShouldProcess(true, false, finalGain, monsterPatExperience))
        {
            if (finalGain > 0)
                ApplyCharacterExperienceGain(state, finalGain);

            CreditPetGrowthFromMonsterKill(state, monsterPatExperience);

            var healthGain = MonsterKillHealthGainCalculator.ComputeHealthValueGain(monsterLifeValue);
            var newLife = MonsterKillHealthGainCalculator.ComputeNewLife(state.Life, state.MaxLife, healthGain);
            if (newLife != state.Life)
            {
                state.Life = newLife;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
            }
        }

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
            ApplyCharacterExperienceGain(member, bonus);
    }

    private void ApplyCharacterExperienceGain(PlayerRuntimeState target, int gain)
    {
        if (HighLevelExperienceResolver.AppliesAt(target.Level))
        {
            ApplyHighLevelExperienceGain(target, gain);
            return;
        }

        var previousExperience = target.Experience;
        target.Experience += gain;
        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        var levelUp = LevelProgressionCalculator.ResolveLevelUp(previousExperience, gain, worldData.LevelsByLevel);
        if (levelUp.LeveledUp)
        {
            var previousLevel = target.Level;
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
                target.StatDex, target.Level, target.Tribe, target.PreviousTribe, target.Title, target.Halo,
                target.RebirthCount, target.Level2);
            var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, target.Buffs,
                petContribution, target);

            target.Stats = stats;
            target.MaxLife = stats.MaxLife;
            target.MaxMana = stats.MaxMana;

            if (target.Life > 0)
            {
                target.Life = stats.MaxLife;
                target.Mana = stats.MaxMana;
            }

            target.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

            BroadcastAvatarStateFlag(target, LevelUpAvatarChangeInfoSort, target.Level, target.StatPoints,
                target.SkillPoints);
            BroadcastAvatarStateFlag(target, LevelUpAvatarChangeInfoSort, target.Level, target.StatPoints,
                target.SkillPoints);

            var armedMilestone = LevelMilestoneBonus.ResolveHighestMilestoneCrossed(previousLevel, target.Level);
            if (armedMilestone > 0)
            {
                target.BonusItemLevel = armedMilestone;
                target.BonusItemValue = true;
                BroadcastAvatarStateFlag(target, BonusItemAvatarChangeInfoSort, 1, armedMilestone, 0);
            }
        }

        target.Session.Send(new AvatarStatUpdateResponse
            { Sort = ExperienceStatSort, Value = (int)target.Experience, Value2 = 0 });
    }

    private void CreditPetGrowthFromMonsterKill(PlayerRuntimeState target, int monsterPatExperience)
    {
        var scaledPetExperience = PetKillExperienceScalingCalculator.ComputeScaledAmount(
            monsterPatExperience,
            0f,
            target.PetExpX2Time > 0,
            target.PremiumExpireUtc >= DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        if (scaledPetExperience < 1)
            return;

        CreditPetGrowth(target, scaledPetExperience);
    }

    private void CreditPetGrowthFromPvpKill(PlayerRuntimeState target, int petExpGain)
    {
        if (petExpGain < 1)
            return;

        CreditPetGrowth(target, petExpGain);
    }

    private void CreditPetGrowth(PlayerRuntimeState target, int requestedPetExperience)
    {
        var equipmentContainer = target.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        var credited = PetExperienceCreditResolver.Resolve(petItemId, target.PetGrowth, target.PetActivity,
            requestedPetExperience, worldData.ItemsById);

        if (!credited.IsEligible || (credited.CreditedAmount == 0 && !credited.ReactivationApplied))
            return;

        target.PetGrowth = credited.NewGrowth;
        target.PetActivity = (byte)credited.NewActivity;
        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    public void ApplyQuestKillProgress(int killerCharacterId, int monsterId,
        IReadOnlyList<int>? partyMemberIds = null)
    {
        if (_players.TryGetValue(killerCharacterId, out var killerState))
            ApplyQuestKillProgressToOne(killerState, monsterId, false);

        if (partyMemberIds is not { Count: > 0 })
            return;

        foreach (var memberId in partyMemberIds)
        {
            if (memberId == killerCharacterId)
                continue;
            if (!_players.TryGetValue(memberId, out var memberState) || memberState.IsMovingZone ||
                memberState.IsDead)
                continue;

            ApplyQuestKillProgressToOne(memberState, monsterId, true);
        }
    }

    private void ApplyQuestKillProgressToOne(PlayerRuntimeState state, int monsterId, bool isPartyRelay)
    {
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
                    state.Session.Send(new QuestProgressResponse
                    {
                        Sort = isPartyRelay ? QuestKillCreditRelayOrArchetype5Sort : QuestKillCreditArchetype1SoloSort,
                        Page = 0,
                        Index = 0,
                        XPost = 0,
                        YPost = 0
                    });
                }

                break;
            case 5:
                if (state.QuestKillCounter < 1)
                {
                    state.QuestKillCounter++;
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                    state.Session.Send(new QuestProgressResponse
                    {
                        Sort = QuestKillCreditRelayOrArchetype5Sort, Page = 0, Index = 0, XPost = 0, YPost = 0
                    });
                }

                break;
        }
    }
}
