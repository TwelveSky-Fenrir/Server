using System.Buffers;
using System.Threading.Channels;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.Stats;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int CombinedLevelGapCap = 13;

    private const int WarPointStatSort = 905;

    private const int BloodPointAvatarChangeInfoSort = 300;

    private const int DoubleKillNumTimeChargeStatSort = 4;

    private const int DoubleKillExpTimeChargeStatSort = 5;

    private const int ExperienceStatSort = 13;

    private const int TeacherPointStatSort = 6;

    private const long TeacherPointCeiling = 2_000_000_000L;

    private const int MonsterKillExperienceLevelGapCap = 9;

    private const int LevelUpAvatarChangeInfoSort = 1;

    private const int BonusItemAvatarChangeInfoSort = 107;

    private const int QuestKillCreditArchetype1SoloSort = 6;

    private const int QuestKillCreditRelayOrArchetype5Sort = 8;

    private const int CombatInboxCapacity = 4096;

    private const int TribeSymbolFirstAttackEventCode = 41;

    private const int AllianceStoneFirstAttackEventCode = 48;


    private const int PvpKillDropSort = 11;

    private const int PvpKillDropMinimumLevel = 135;

    private const int YangGokPvpDropRatePercent = 35;

    private const int RegularWarDropStreakRequirement = 10;

    private const float Map38TelebornKillRadius = 1000f;

    private static readonly (float X, float Y, float Z)[] Map38TelebornKillControlPoints =
    [
        (-1728f, -26f, 9657f), (-10f, -26f, 11223f), (1169f, -26f, 11209f), (2739f, -26f, 9660f)
    ];

    private static readonly int[] YangGokPvpDropBoxes = [602, 601, 2249, 8005, 8112];

    private static readonly int[] KillDropElixirs = [506, 507, 508, 509, 578, 579];

    private static readonly int[] KillDropAnimalTier5 = [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326];

    private static readonly int[] KillDropAnimalTier10 = [1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327];

    private static readonly KillDropEntry[] RegularWarDropTierA =
        [KillDropEntry.Item(720)];

    private static readonly KillDropEntry[] RegularWarDropTierB =
    [
        KillDropEntry.Item(721), KillDropEntry.Item(724), KillDropEntry.Animal5,
        KillDropEntry.Item(722), KillDropEntry.Item(1449)
    ];

    private static readonly KillDropEntry[] RegularWarDropTierC =
    [
        KillDropEntry.Item(694), KillDropEntry.Item(1103), KillDropEntry.Item(1124),
        KillDropEntry.Item(1166), KillDropEntry.Item(8102)
    ];

    private static readonly KillDropEntry[] RegularWarDropTierD =
        [KillDropEntry.Elixir, KillDropEntry.Item(695), KillDropEntry.Item(696)];

    private static readonly KillDropEntry[] CityDropTierA =
        [KillDropEntry.Animal10, KillDropEntry.Item(724), KillDropEntry.Item(721)];

    private static readonly KillDropEntry[] CityDropTierB =
        [KillDropEntry.Animal5, KillDropEntry.Item(8102), KillDropEntry.Item(720), KillDropEntry.Item(1166)];

    private static readonly KillDropEntry[] CityDropTierC =
    [
        KillDropEntry.Binary(694, 1449), KillDropEntry.Item(1103), KillDropEntry.Item(1124), KillDropEntry.Item(722)
    ];

    private static readonly KillDropEntry[] CityDropTierD =
        [KillDropEntry.Elixir, KillDropEntry.Binary(695, 696)];

    private readonly Channel<CombatCommand> _combatInbox = Channel.CreateBounded<CombatCommand>(
        new BoundedChannelOptions(CombatInboxCapacity)
            { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly List<int> _combatNeighborScratch = [];

    private readonly HashSet<int> _combatRecipientScratch = [];

    private readonly KillCooldownTracker _killCooldownTracker = killCooldownTracker ?? new KillCooldownTracker();

    private readonly HashSet<int> _pvmAttackRecipientScratch = [];

    private readonly QuestCatalog _questCatalog = questCatalog ?? new QuestCatalog(worldData);

    private readonly Dictionary<int, int> _regularWarKillDropStreak = new();

    private readonly MonsterSpawnScheduler? _stoneSiegeAnnouncer =
        simulationSystems.OfType<MonsterSpawnScheduler>().FirstOrDefault();

    private readonly Zone175LabyrinthSystem? _zone175LabyrinthSystem =
        simulationSystems.OfType<Zone175LabyrinthSystem>().FirstOrDefault();

    private int _lastObservedRegularWarCycle;

    private byte? _regularWarSmallestPresentTribe;

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

    private void ApplySenderLocation(PlayerRuntimeState state, AttackForProtocol attackInfo)
    {
        var claimedX = attackInfo.SenderLocation[0];
        var claimedY = attackInfo.SenderLocation[1];
        var claimedZ = attackInfo.SenderLocation[2];

        if (movementRules.IsPlausible(state.DeclaredMoveAnchorX, state.DeclaredMoveAnchorY, state.DeclaredMoveAnchorZ,
                claimedX, claimedY, claimedZ, Geometry, options.MaxAttackPacketPositionDelta))
        {
            state.PosX = claimedX;
            state.PosY = claimedY;
            state.PosZ = claimedZ;
            state.ImplausibleAttackPositionStreak = 0;
        }
        else
        {
            state.ImplausibleAttackPositionStreak++;

            logger.LogWarning(
                "Zone {MapId}: character {CharacterId} claimed an implausible attack-packet position, REJECTED -- " +
                "server position kept (consecutive={Streak}) Case={Case} Anchor=({AnchorX},{AnchorY},{AnchorZ}) " +
                "To=({ToX},{ToY},{ToZ}) GeometryLoaded={GeometryLoaded}",
                MapId, state.CharacterId, state.ImplausibleAttackPositionStreak, attackInfo.Case,
                state.DeclaredMoveAnchorX, state.DeclaredMoveAnchorY, state.DeclaredMoveAnchorZ,
                claimedX, claimedY, claimedZ, Geometry is not null);
        }

        var newCell = _grid.CellOf(state.PosX, state.PosZ);
        _grid.Move(state.CharacterId, state.CurrentCell, newCell, state.PosX, state.PosY, state.PosZ);
        state.CurrentCell = newCell;
    }

    private void ApplyCombatCommand(in CombatCommand command)
    {
        if (_players.TryGetValue(command.AttackerCharacterId, out var sender) && sender.IsMovingZone)
            return;

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

        var outcome = CombatResolver.ResolveEnemyTribeAttack(attackerSnapshot, defenderSnapshot,
            command.AttackInfo, _clock, attackSkill, _random,
            ZonePvpZoneCatalog.AllowsEnemyTribeAttack(MapId),
            ZonePvpZoneCatalog.IsSameTribeAttackExempt(MapId),
            ZonePvpZoneCatalog.IsNewbieProtectionZone(MapId),
            defenderState.PshopOpen, defenderState.ActionSort,
            worldState?.GetAllyOf(attackerSnapshot.Tribe),
            worldState?.GetTribeFormationAbility(attackerSnapshot.Tribe) ?? FormationCombatResolver.NoFormation,
            worldState?.GetTribeFormationAbility(defenderSnapshot.Tribe) ?? FormationCombatResolver.NoFormation,
            attackerState.AttackBudgetEnforced, attackerState.ActionSkillNumber,
            attackerState.ActionSkillGradeNum1 + attackerState.ActionSkillGradeNum2);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            ConsumeChargeBuff(attackerState);

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

            TryApplyDarkAttackPotionProc(attackerState, defenderState);
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

        if (!outcome.Hit)
        {
            attackerState.Session.Send(response);
            return;
        }

        var recipients = CombatRecipients(attackerState, defenderState);
        BroadcastAttackResult(recipients, response);

        if (reflectFired)
            return;

        defenderState.Life -= realDamage;
        defenderState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (defenderState.Life <= 0)
        {
            var killType = ClassifyPvpKillType(command.AttackInfo.AttackActionValue2, false);
            ApplyPvpKillRewards(attackerState, defenderState, killType == KillCpType.Stun);
            RegisterRegularWarKill(attackerState, killType == KillCpType.Stun);
            RecordEnemyKillForFeed(attackerState, defenderState, killType == KillCpType.Stun,
                regularWarActiveMapTracker?.IsBattleInProgress(MapId) == true ||
                MapId == KillFeedZoneCatalog.FfaMapNumber);
            ApplyDeath(defenderState.CharacterId, DeathCause.PlayerKill,
                (attackerState.PosX, attackerState.PosZ));
        }
    }

    private void RegisterRegularWarKill(PlayerRuntimeState attackerState, bool isStunTrigger)
    {
        if (regularWarActiveMapTracker is null)
            return;

        ClearKillFeedLeaderboardOnNewWarCycle();

        if (isStunTrigger)
            return;

        regularWarActiveMapTracker.RegisterKill(MapId, attackerState.Tribe, attackerState.CharacterId);
    }

    private void ClearKillFeedLeaderboardOnNewWarCycle()
    {
        if (regularWarActiveMapTracker is null)
            return;

        var warCycle = regularWarActiveMapTracker.GetWarCycle(MapId);
        if (warCycle == _lastObservedRegularWarCycle)
            return;

        _lastObservedRegularWarCycle = warCycle;
        ClearKillFeedLeaderboard();
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

        var symbolBattleActive = worldState?.World.TribeSymbolBattle ?? false;

        var zoneRuntime = new PvpKillRewardZoneRuntimeState(
            symbolBattleActive,
            Zone195TimeEventGate.IsOpen,
            false,
            IsMap38DailyMissionKillWindowOpen(attackerState, symbolBattleActive));
        var profile = PvpKillExtendedRewardZones.TryResolve(MapId, isStunTrigger, zoneRuntime)
                      ?? PvpKillRewardZoneCatalog.Resolve(MapId, isStunTrigger);

        if (profile.GrantDailyMissionProgress)
        {
            attackerState.MissionKillOtherTribe =
                Math.Min(attackerState.MissionKillOtherTribe + 1, KillCooldownTracker.MissionKillOtherTribeCap);
            attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        ApplyPvpKillContributionPointFormula(attackerState, profile);
        ApplyPvpKillHeroPoints(attackerState, profile, attackerCombinedLevel);
        ApplyPvpKillExperience(attackerState, profile, attackerCombinedLevel, defenderCombinedLevel);
        DecrementDoubleKillNumTime2OnKill(attackerState);

        if (profile.GrantDrop)
            DropItemsForKillOtherTribe(attackerState, defenderState);
    }

    private bool IsMap38DailyMissionKillWindowOpen(PlayerRuntimeState attackerState, bool symbolBattleActive)
    {
        if (MapId != PvpKillExtendedRewardZones.DtmZoneId)
            return false;

        if (!symbolBattleActive)
            return false;

        var radiusSquared = Map38TelebornKillRadius * Map38TelebornKillRadius;
        foreach (var point in Map38TelebornKillControlPoints)
        {
            var dx = attackerState.PosX - point.X;
            var dy = attackerState.PosY - point.Y;
            var dz = attackerState.PosZ - point.Z;
            if (dx * dx + dy * dy + dz * dz <= radiusSquared)
                return false;
        }

        return true;
    }

    private void ApplyPvpKillContributionPointFormula(PlayerRuntimeState attackerState,
        PvpKillZoneRewardProfile profile)
    {
        if (!profile.GrantContributionPoints && MapId != PvpKillRewardZoneCatalog.FfaMapNumber)
            return;

        var hasCrossTribeAddTimeEffect = attackerState.StateTimeEffect == 300;
        var towerControlBonus = towerWar?.GetTribeBonus(attackerState.Tribe).CpForPvpBonus ?? 0;

        var baseAmount = PvpKillContributionPointCalculator.ComputeBaseAmount(
            attackerState.PremiumExpireUtc > 0,
            attackerState.WarriorScroll > 0,
            PvpKillContributionPointBonuses.ComputePerUserAddValue(hasCrossTribeAddTimeEffect),
            0,
            towerControlBonus,
            PvpKillContributionPointBonuses.ComputeGameWideAddValue(options.CrossTribeCpAddValue));

        var serverHomeTribe = TryGetCityOwningTribe(MapId, out var owningTribe) ? owningTribe : -1;
        baseAmount += PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            MapId, attackerState.Tribe,
            _regularWarSmallestPresentTribe ?? -1,
            attackerState.Level,
            worldState?.World.TribeSymbolBattle ?? false,
            serverHomeTribe);

        ApplyDoubleAwardCpGrant(attackerState, baseAmount);
    }

    private void ApplyDoubleAwardCpGrant(PlayerRuntimeState attackerState, int baseAmount)
    {
        var firstGrant = PvpKillContributionPointCalculator.ClampGrant(attackerState.ContributionPoints,
            baseAmount, PvpKillContributionPointCalculator.ContributionPointHardCap);
        GrantContributionPoints(attackerState.CharacterId, firstGrant);

        var totalChanged = firstGrant != 0;

        var doubleAward = PvpKillContributionPointCalculator.ResolveDoubleAward(attackerState.ContributionPoints,
            baseAmount, PvpKillContributionPointCalculator.ContributionPointHardCap, attackerState.DoubleKillNumTime);
        if (doubleAward.Fired)
        {
            attackerState.DoubleKillNumTime = doubleAward.NewDoubleKillNumTime;
            attackerState.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = DoubleKillNumTimeChargeStatSort, Value = doubleAward.NewDoubleKillNumTime, Value2 = 0
            });
            GrantContributionPoints(attackerState.CharacterId, doubleAward.GrantedAmount);
            totalChanged = true;
        }

        if (totalChanged)
            attackerState.Session.Send(new AvatarStatUpdateResponse
                { Sort = ContributionPointStatSort, Value = attackerState.ContributionPoints, Value2 = 0 });
    }

    private void HandleSetRegularWarSmallestTribe(byte tribeId)
    {
        _regularWarSmallestPresentTribe = tribeId;
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
        if (!profile.GrantExperience)
            return;

        var scaledBase = PvpKillExperienceScaling.Scale(
            PvpKillExperienceBaseTable.Lookup(defenderCombinedLevel),
            attackerCombinedLevel,
            defenderCombinedLevel);
        var zoneMultiplier = PvpKillExperienceScaling.ResolveZoneMultiplier(
            RegularWarMapCatalog.TryGet(MapId, out _),
            options.CrossTribeXpRatio);
        var hasDoubleExpCharge = attackerState.DoubleKillExpTime > 0;
        var gain = PvpKillExperienceCalculator.ComputeGain(
            scaledBase,
            attackerCombinedLevel,
            defenderCombinedLevel,
            attackerState.WarriorScroll > 0,
            hasDoubleExpCharge,
            zoneMultiplier);

        if (gain > 0)
            ApplyCharacterExperienceGain(attackerState, gain);

        if (hasDoubleExpCharge)
        {
            attackerState.DoubleKillExpTime--;
            attackerState.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = DoubleKillExpTimeChargeStatSort, Value = attackerState.DoubleKillExpTime, Value2 = 0
            });
            attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (HasActivePetEquipped(attackerState))
            CreditPetGrowthFromPvpKill(attackerState,
                PvpKillPetExperienceCalculator.ComputeGain(attackerState.Level));

        ApplyPvpKillMountExperience(attackerState);
    }

    private static bool HasActivePetEquipped(PlayerRuntimeState state)
    {
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        return petItemId > 0 && state.PetActivity > 0;
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

    private void DecrementDoubleKillNumTime2OnKill(PlayerRuntimeState attackerState)
    {
        if (attackerState.DoubleKillNumTime2 <= 0)
            return;

        attackerState.DoubleKillNumTime2 = Math.Max(0, attackerState.DoubleKillNumTime2 - 1);
        attackerState.Session.Send(new AvatarStatUpdateResponse
            { Sort = 30, Value = attackerState.DoubleKillNumTime2, Value2 = 0 });
        attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    private void DropItemsForKillOtherTribe(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState)
    {
        if (attackerState.Level < PvpKillDropMinimumLevel || defenderState.Level < PvpKillDropMinimumLevel)
            return;

        if (MapId == PvpKillExtendedRewardZones.DtmZoneId)
        {
            if (_random.NextInt32(100) < YangGokPvpDropRatePercent)
                TrySpawnPvpKillDrop(YangGokPvpDropBoxes[_random.NextInt32(YangGokPvpDropBoxes.Length)],
                    attackerState, defenderState);
            return;
        }

        if (RegularWarMapCatalog.TryGet(MapId, out _))
        {
            var streak = _regularWarKillDropStreak.GetValueOrDefault(attackerState.CharacterId) + 1;
            if (streak >= RegularWarDropStreakRequirement)
            {
                _regularWarKillDropStreak[attackerState.CharacterId] = 0;
                var itemId = ResolveKillDropItemId(PickKillDropTierMember(
                    RegularWarDropTierA, RegularWarDropTierB, RegularWarDropTierC, RegularWarDropTierD));
                TrySpawnPvpKillDrop(itemId, attackerState, defenderState);
            }
            else
            {
                _regularWarKillDropStreak[attackerState.CharacterId] = streak;
            }
        }

        if (TryGetCityOwningTribe(MapId, out var cityOwningTribe) && attackerState.Tribe != cityOwningTribe)
        {
            var itemId = ResolveKillDropItemId(PickKillDropTierMember(
                CityDropTierA, CityDropTierB, CityDropTierC, CityDropTierD));
            TrySpawnPvpKillDrop(itemId, attackerState, defenderState);
        }
    }

    private KillDropEntry PickKillDropTierMember(KillDropEntry[] tierA, KillDropEntry[] tierB,
        KillDropEntry[] tierC, KillDropEntry[] tierD)
    {
        var tier = _random.NextInt32(1000) switch
        {
            < 25 => tierA,
            < 100 => tierB,
            < 400 => tierC,
            _ => tierD
        };
        return tier[_random.NextInt32(tier.Length)];
    }

    private int ResolveKillDropItemId(KillDropEntry entry)
    {
        return entry.Kind switch
        {
            KillDropEntryKind.Item => entry.First,
            KillDropEntryKind.Binary => _random.NextInt32(2) == 0 ? entry.First : entry.Second,
            KillDropEntryKind.Elixir => KillDropElixirs[_random.NextInt32(KillDropElixirs.Length)],
            KillDropEntryKind.Animal5 => KillDropAnimalTier5[_random.NextInt32(KillDropAnimalTier5.Length)],
            KillDropEntryKind.Animal10 => KillDropAnimalTier10[_random.NextInt32(KillDropAnimalTier10.Length)],
            _ => 0
        };
    }

    private void TrySpawnPvpKillDrop(int itemId, PlayerRuntimeState killer, PlayerRuntimeState victim)
    {
        if (itemId <= 0 || !worldData.ItemsById.ContainsKey(itemId))
            return;

        SpawnGroundItem(itemId, 1, victim.PosX, victim.PosY, victim.PosZ, killer.Name, "", PvpKillDropSort,
            victim.DungeonInstanceId);
    }

    private static bool TryGetCityOwningTribe(short mapId, out byte owningTribe)
    {
        owningTribe = mapId switch
        {
            1 => 0,
            6 => 1,
            11 => 2,
            140 => 3,
            _ => byte.MaxValue
        };
        return owningTribe != byte.MaxValue;
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

        if (!AttackPacketBudget.TryConsume(attackerState, command.AttackInfo.AttackActionValue4))
            return;

        if (monster.AiState is MonsterAiState.Spawning or MonsterAiState.Dead or MonsterAiState.ReturnToSpawn)
            return;

        var towerIndex = TowerZoneIndexTable.GetTowerIndex(MapId);
        var isTowerGuardian = towerIndex >= 0 && monster.ServerIndex == TowerWarState.GuardianServerIndex(towerIndex);
        var targetCategoryEligible = (!isTowerGuardian || CanAttackTowerGuardian(attackerState.Tribe, towerIndex)) &&
                                     CanAttackSpecialSortTarget(monster, attackerState.Tribe);

        var attackerSnapshot = ToCombatantSnapshot(attackerState);

        var attackSkill = command.AttackInfo.AttackActionValue1 == 2 &&
                          worldData.SkillsById.TryGetValue(command.AttackInfo.AttackActionValue2,
                              out var skillDef)
            ? skillDef
            : null;

        var outcome = MonsterCombatResolver.ResolvePvmAttack(attackerSnapshot, monster, command.AttackInfo, _clock,
            attackSkill, _random, attackerState.AttackBudgetEnforced, attackerState.ActionSkillNumber,
            attackerState.ActionSkillGradeNum1 + attackerState.ActionSkillGradeNum2,
            tribeSymbolCombatModifiers?.GetDamageDownPenalty(attackerSnapshot.Tribe) ?? 0f,
            tribeSymbolCombatModifiers?.GetDamageUpBonusIncrementCount(attackerSnapshot.Tribe) ?? 0,
            targetCategoryEligible,
            zone195NokSanState?.GetMonsterDamageBonus(attackerSnapshot.Tribe) ?? 0);

        var attackReachedMonster = !outcome.Rejected || outcome.RejectReason is
            AttackRejectReason.AntiCheatEchoMismatch or AttackRejectReason.InvalidAttackModeSelector
            or AttackRejectReason.AttackerHasNoAttackSuccess;
        if (attackReachedMonster)
            monster.RegisterAcquisition(attackerState.CharacterId, attackerState);

        if (outcome.ChargeConsumed)
            ConsumeChargeBuff(attackerState);

        if (outcome.Rejected)
            return;

        ApplyStoneFirstAttackSideEffects(monster, attackerState);

        if (!outcome.Hit)
        {
            attackerState.Session.Send(new AttackResponse
            {
                AttackInfo = command.AttackInfo with { AttackResultValue = 0 }
            });
            return;
        }

        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var zone175BossDamageEcho = ResolveZone175BossDamageWireEcho(attackerState, monster, outcome.DamageApplied);
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = 1 + attackerWeaponItemId,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.ViewDamage,
                AttackRealDamageValue = outcome.DamageApplied,
                AttackActionValue4 = zone175BossDamageEcho ?? command.AttackInfo.AttackActionValue4
            }
        };

        if (monster.SpecialSort == MonsterSpecialSort.TribeSymbolStone)
            monster.AddTribeSymbolDamage(attackerState.Tribe, outcome.DamageApplied);

        TryDamageMonster(monster.ServerIndex, outcome.DamageApplied, attackerState.CharacterId, out var monsterDied,
            out _, outcome.Critical);

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

        BroadcastAttackResult(_pvmAttackRecipientScratch, response, attackerState.DungeonInstanceId);

        if (!monsterDied)
            TryApplyPvmFlinch(monster, outcome.DamageApplied);

        if (isTowerGuardian)
            ApplyTowerGuardianHitSideEffects(towerIndex, attackerState);
    }

    private int? ResolveZone175BossDamageWireEcho(PlayerRuntimeState attackerState, MonsterEntity monster,
        int realDamage)
    {
        if (_zone175LabyrinthSystem is not { } zone175System || !zone175System.IsZone175Map(MapId))
            return null;

        if (!Zone175RewardTables.IsWaveBossSpecialType(monster.Template.SpecialType))
            return 0;

        attackerState.Zone175BossDamage += realDamage;
        return unchecked((int)attackerState.Zone175BossDamage);
    }

    private void ApplyStoneFirstAttackSideEffects(MonsterEntity monster, PlayerRuntimeState attackerState)
    {
        switch (monster.SpecialSort)
        {
            case MonsterSpecialSort.TribeSymbolStone when !monster.TribeSymbolFirstAttackArmed:
                if (TribeSymbolFirstAttackSlotOf(monster.Template.SpecialType) is not { } symbolSlot)
                    return;

                monster.TribeSymbolFirstAttackArmed = true;
                monster.TribeSymbolFirstAttackElapsedLegacyTicks = 0;
                _stoneSiegeAnnouncer?.AnnounceStoneFirstAttack(TribeSymbolFirstAttackEventCode, symbolSlot,
                    attackerState.Tribe, attackerState.Name);
                return;

            case MonsterSpecialSort.AllianceStone when !monster.AllianceStoneFirstAttackArmed:
                if (AllianceStoneFirstAttackSlotOf(monster.Template.SpecialType) is not { } allianceSlot)
                    return;

                monster.AllianceStoneFirstAttackArmed = true;
                monster.AllianceStoneFirstAttackElapsedLegacyTicks = 0;
                _stoneSiegeAnnouncer?.AnnounceStoneFirstAttack(AllianceStoneFirstAttackEventCode, allianceSlot,
                    attackerState.Tribe, attackerState.Name);
                return;
        }
    }

    private static int? TribeSymbolFirstAttackSlotOf(byte specialType)
    {
        return specialType switch
        {
            11 => 0,
            12 => 1,
            13 => 2,
            28 => 3,
            14 => 4,
            _ => null
        };
    }

    private static int? AllianceStoneFirstAttackSlotOf(byte specialType)
    {
        return specialType switch
        {
            31 => 0,
            32 => 1,
            33 => 2,
            34 => 3,
            _ => null
        };
    }

    private bool CanAttackSpecialSortTarget(MonsterEntity monster, byte attackerTribe)
    {
        return monster.SpecialSort switch
        {
            MonsterSpecialSort.Standard => true,
            MonsterSpecialSort.TribeSymbolStone => CanAttackTribeSymbolStone(monster.Template.SpecialType,
                attackerTribe),
            MonsterSpecialSort.Inert => monster.Template.SpecialType switch
            {
                21 => CanAttackTribeOwnedStone(attackerTribe, 0),
                22 => CanAttackTribeOwnedStone(attackerTribe, 1),
                23 => CanAttackTribeOwnedStone(attackerTribe, 2),
                29 => CanAttackTribeOwnedStone(attackerTribe, 3),
                _ => true
            },
            MonsterSpecialSort.AllianceStone => monster.Template.SpecialType switch
            {
                31 => CanAttackTribeOwnedStone(attackerTribe, 0),
                32 => CanAttackTribeOwnedStone(attackerTribe, 1),
                33 => CanAttackTribeOwnedStone(attackerTribe, 2),
                34 => CanAttackTribeOwnedStone(attackerTribe, 3),
                _ => true
            },
            MonsterSpecialSort.TribeGuard => monster.Template.Type switch
            {
                6 => CanAttackTribeOwnedStone(attackerTribe, 0),
                7 => CanAttackTribeOwnedStone(attackerTribe, 1),
                8 => CanAttackTribeOwnedStone(attackerTribe, 2),
                9 => CanAttackTribeOwnedStone(attackerTribe, 3),
                _ => true
            },
            MonsterSpecialSort.CarThrower => monster.Template.SpecialType switch
            {
                35 => CanAttackTribeOwnedStone(attackerTribe, 0),
                36 => CanAttackTribeOwnedStone(attackerTribe, 1),
                37 => CanAttackTribeOwnedStone(attackerTribe, 2),
                38 => CanAttackTribeOwnedStone(attackerTribe, 3),
                _ => true
            },
            MonsterSpecialSort.Tower => CanAttackTowerBySpecialSort(attackerTribe),
            _ => false
        };
    }

    private bool CanAttackTowerBySpecialSort(byte attackerTribe)
    {
        var towerIndex = TowerZoneIndexTable.GetTowerIndex(MapId);
        return towerIndex >= 0 && CanAttackTowerGuardian(attackerTribe, towerIndex);
    }

    private bool CanAttackTribeOwnedStone(byte attackerTribe, byte stoneTribe)
    {
        return attackerTribe != stoneTribe && worldState?.GetAllyOf(stoneTribe) != attackerTribe;
    }

    private bool CanAttackTribeSymbolStone(byte specialType, byte attackerTribe)
    {
        var allianceTribe = worldState?.GetAllyOf(attackerTribe);

        return specialType switch
        {
            11 => CanAttackTribeSymbolSlot(0, attackerTribe, allianceTribe),
            12 => CanAttackTribeSymbolSlot(1, attackerTribe, allianceTribe),
            13 => CanAttackTribeSymbolSlot(2, attackerTribe, allianceTribe),
            28 => CanAttackTribeSymbolSlot(3, attackerTribe, allianceTribe),
            14 => CanAttackMonsterSymbolStone(attackerTribe, allianceTribe),
            _ => true
        };
    }

    private bool CanAttackTribeSymbolSlot(byte slotTribe, byte attackerTribe, byte? allianceTribe)
    {
        if (worldState is null || !worldState.World.TribeSymbolBattle)
            return false;

        var owner = worldState.GetTribeSymbolOwner(slotTribe);
        if (owner == attackerTribe || owner == allianceTribe)
            return false;

        var homeTribe = MapId switch
        {
            2 => 0,
            7 => 1,
            11 => 2,
            141 => 3,
            _ => -1
        };

        return homeTribe < 0 || (attackerTribe != homeTribe && allianceTribe != homeTribe);
    }

    private bool CanAttackMonsterSymbolStone(byte attackerTribe, byte? allianceTribe)
    {
        var monsterSymbolTribe = worldState?.World.MonsterSymbol;
        return monsterSymbolTribe != attackerTribe && (allianceTribe is null || monsterSymbolTribe != allianceTribe);
    }

    private bool CanAttackTowerGuardian(byte attackerTribe, int towerIndex)
    {
        var owningTribe = TowerZoneIndexTable.GetOwningTribe(MapId);
        var towerActivelyBuilt = towerWar?.GetPhase(towerIndex) == TowerSiegePhase.Active;

        return TowerFriendlyFireGate.CanAttackGuardian(attackerTribe, owningTribe, towerActivelyBuilt);
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
                    if (TryGetZoneWideBroadcastRecipient(player.CharacterId, out var clientSession))
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

    private void BroadcastAttackResult(IReadOnlyCollection<int> recipientCharacterIds, in AttackResponse response,
        int? anchorInstanceId = null)
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
                    if (TryGetBroadcastRecipient(id, out var recipient, out var clientSession) &&
                        IsVisibleAcrossDungeonInstance(anchorInstanceId, recipient.DungeonInstanceId))
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
        IReadOnlyList<int>? partyMemberIds = null, int monsterPatExperience = 0, int monsterLifeValue = 0,
        int monsterItemLevel = 0)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        if (monsterGeneralExperience < 1)
            return;

        if (IsZone200TypeZone)
            return;

        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(state.Level + state.Level2);

        if (fixedLevel - monsterLevel > MonsterKillExperienceLevelGapCap)
            return;

        var rawGain = ExperienceFormulas.ComputeMonsterKillExperience(fixedLevel, monsterLevel,
            monsterGeneralExperience);

        var xpBonusRatio = towerWar?.GetTribeBonus(state.Tribe).XpRatio ?? 0f;
        if (xpBonusRatio > 0f && monsterGeneralExperience >= 1)
            rawGain += (int)(monsterGeneralExperience * xpBonusRatio);

        if (state.FightingGodForDestroy > 0 && state.Level < ExperienceFormulas.RebirthDivisorLevelThreshold &&
            state.Level2 == 0)
            rawGain *= 2;
        if (state.DoubleExpTime1 > 0)
            rawGain *= 2;
        if (state.DoubleExpTime2 > 0)
            rawGain *= 2;
        if (IsZone126TypeZone)
            rawGain *= 2;
        if (state.PremiumExpireUtc > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            rawGain *= 2;

        if (monsterGeneralExperience >= 1 && worldState?.World.Zone038WinTribe == state.Tribe)
            rawGain += (int)(monsterGeneralExperience * 0.1f);

        const int MentorMaxStudentLevel = 113;
        if (monsterGeneralExperience >= 1
            && fixedLevel - monsterLevel <= MonsterKillExperienceLevelGapCap
            && partyMemberIds is null or { Count: 0 }
            && state.Level < MentorMaxStudentLevel
            && state.TeacherCharacterId is { } teacherId
            && _players.TryGetValue(teacherId, out var teacher)
            && !teacher.IsMovingZone
            && !teacher.IsDead
            && teacher.StudentCharacterId == state.CharacterId)
        {
            rawGain += (int)(monsterGeneralExperience * 0.5f) * options.TeacherPointUpRatio;
            CreditTeacherPointsFromStudentKill(teacher, monsterItemLevel);
        }

        if (options.GeneralExpUpRatio > 1f)
            rawGain = (int)(rawGain * options.GeneralExpUpRatio);

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

    private void CreditTeacherPointsFromStudentKill(PlayerRuntimeState teacher, int monsterItemLevel)
    {
        var credited = teacher.TeacherPoint + (long)monsterItemLevel * options.TeacherPointUpRatio;
        if (credited >= TeacherPointCeiling)
            return;

        teacher.TeacherPoint = (int)credited;
        teacher.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        teacher.Session.Send(new AvatarStatUpdateResponse
            { Sort = TeacherPointStatSort, Value = teacher.TeacherPoint, Value2 = 0 });
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
                memberState.IsDead || memberState.VisibleState == 0)
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

    private enum KillDropEntryKind : byte
    {
        Item,
        Binary,
        Elixir,
        Animal5,
        Animal10
    }

    private readonly record struct KillDropEntry(KillDropEntryKind Kind, int First, int Second)
    {
        public static readonly KillDropEntry Elixir = new(KillDropEntryKind.Elixir, 0, 0);

        public static readonly KillDropEntry Animal5 = new(KillDropEntryKind.Animal5, 0, 0);

        public static readonly KillDropEntry Animal10 = new(KillDropEntryKind.Animal10, 0, 0);

        public static KillDropEntry Item(int id)
        {
            return new KillDropEntry(KillDropEntryKind.Item, id, 0);
        }

        public static KillDropEntry Binary(int first, int second)
        {
            return new KillDropEntry(KillDropEntryKind.Binary, first, second);
        }
    }
}
