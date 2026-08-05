using System.Buffers.Binary;
using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Wire;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Monsters;

internal sealed class MonsterSpawnSlot
{
    public required MonsterSpawnRegionRowDto Region { get; set; }
    public required MonsterRowDto Monster { get; set; }
    public required int ServerIndex { get; init; }
    public bool Alive { get; set; }
    public bool CorpsePending { get; set; }
    public int RespawnTicksRemaining { get; set; }
    public TimeSpan? RespawnDueAtZoneClock { get; set; }
    public bool HasPersistedDeadline { get; set; }
    public bool IsZone175MissionMonster { get; init; }
    public bool IsZone175WaveBoss { get; init; }
}

internal sealed class MonsterZoneSpawnState
{
    public int DemonLordKillTally;

    public int IgnoreRespawnDelayPending;

    public int SummonStateResetPending;
    public required List<MonsterSpawnSlot> Slots { get; init; }
    public required Dictionary<int, MonsterSpawnSlot> SlotsByServerIndex { get; init; }
    public required MonsterDropRoller DropRoller { get; init; }
    public required Random Random { get; init; }
    public int NextServerIndex { get; set; }
    public bool InitialPopDone { get; set; }
    public int TicksSinceLastScan { get; set; }
    public long AppliedConfigurationRevision { get; set; }
}

public sealed class MonsterSpawnScheduler(
    WorldDataCache worldData,
    GroundItemFactory groundItemFactory,
    Func<Random>? randomFactory = null,
    PartyRegistry? partyRegistry = null,
    Lazy<ZoneEventBroadcaster>? zoneEventBroadcaster = null,
    MonsterBossRespawnTracker? bossRespawnTracker = null,
    TowerWarState? towerWar = null,
    ValleyWarKillRegistry? valleyWarKillRegistry = null,
    BossDropCatalog? bossDropCatalog = null,
    Lazy<ZoneCenterBroadcastIngestor>? siegeIngestor = null,
    ZoneCenterSiegeState? siegeState = null)
    : ISimulationSystem
{
    private const int RegularMonsterTableCapacity = 3400;

    private const int FirstAttackNameOffset = 8;

    private const int FirstAttackNameSize = 13;

    private const int ItemDropUpStateTimeEffect = 120;

    private const short YangGokNormalBossZoneId = 38;

    private const int TowerDropCpGiftCard5ItemId = 691;

    private const int TowerDropCpGiftCard10ItemId = 692;

    private const int TowerDropCpGiftCard15ItemId = 693;

    private const int TowerDropCpGiftCard20ItemId = 694;

    private const int TowerDropBonusItemId = 666;

    private const int TowerDropQuantity = 20;

    private const string SystemDropMasterName = "-System-";

    private const int KillerLocationDropMonsterIdCeiling = 288;

    private static readonly DateTime YangGokNormalBossAliveSentinelUtc = DateTime.MinValue;

    private readonly BossDropCatalog _bossDropCatalog = bossDropCatalog ?? BossDropCatalog.Default;

    private readonly Func<Random> _randomFactory = randomFactory ?? (static () => new Random());

    private readonly ConcurrentDictionary<short, MonsterZoneSpawnState> _stateByZone = new();

    private long _configurationRevision;

    public void RequestConfigurationReload()
    {
        Interlocked.Increment(ref _configurationRevision);
    }

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (legacyTicksElapsed <= 0)
            return;

        const int elapsedLegacyTicks = 1;
        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone.MapId, zone.IsDungeonServerZone));
        var configurationRevision = Volatile.Read(ref _configurationRevision);
        if (state.AppliedConfigurationRevision != configurationRevision)
            RefreshSlotDefinitions(zone.MapId, state, configurationRevision);

        if (!state.InitialPopDone)
        {
            state.InitialPopDone = true;
            foreach (var slot in state.Slots)
                if (!slot.Alive && !slot.CorpsePending &&
                    (!slot.HasPersistedDeadline || IsRespawnDue(slot, zone.MonsterRuntimeClock)))
                    Spawn(zone, slot);
        }

        DrainDeaths(zone, state);

        if (Interlocked.Exchange(ref state.SummonStateResetPending, 0) != 0)
            ApplySummonStateReset(zone, state);

        foreach (var slot in state.Slots)
            if (!slot.Alive && !slot.CorpsePending && slot.RespawnDueAtZoneClock is null &&
                !slot.IsZone175WaveBoss)
                slot.RespawnTicksRemaining = Math.Max(0, slot.RespawnTicksRemaining - elapsedLegacyTicks);

        DrainInvalidations(zone, state);

        state.TicksSinceLastScan += elapsedLegacyTicks;
        if (state.TicksSinceLastScan < SimulationClock.MonsterRespawnScanLegacyTicks)
            return;

        state.TicksSinceLastScan = 0;
        var ignoreRespawnDelay = Interlocked.Exchange(ref state.IgnoreRespawnDelayPending, 0) != 0;
        foreach (var slot in state.Slots)
            if (!slot.Alive && !slot.CorpsePending && !slot.IsZone175WaveBoss &&
                (ignoreRespawnDelay || IsRespawnDue(slot, zone.MonsterRuntimeClock)))
                Spawn(zone, slot);
    }

    public void RequestSummonStateReset(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone.MapId, zone.IsDungeonServerZone));
        Interlocked.Exchange(ref state.SummonStateResetPending, 1);
    }

    public bool TryLoadZone175MissionStage(Zone zone, int stage)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (stage is < 1 or > Zone175RewardTables.WaveCount ||
            !worldData.ZonesByNumber.TryGetValue(zone.MapId, out var zoneDefinition))
            return false;

        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone.MapId, zone.IsDungeonServerZone));
        if (state.Slots.Any(static slot => slot.IsZone175MissionMonster))
            return false;

        var resolved = new List<(MonsterSpawnRegionRowDto Region, MonsterRowDto Monster, int Count)>();
        var totalRequested = 0;
        var containsExpectedBoss = false;
        foreach (var region in zoneDefinition.Zone175MissionSpawnRegions)
        {
            if (!Zone175MissionSpawnRegionFile.TryGetStage(region.SourceFileName, out var sourceStage) ||
                sourceStage != stage || region.MonsterId is not { } monsterId ||
                !worldData.MonstersById.TryGetValue(monsterId, out var definition))
                continue;

            var count = Math.Max(0, region.Number);
            if (count == 0)
                continue;

            containsExpectedBoss |= definition.Monster.SpecialType == Zone175RewardTables.WaveBossSpecialType(stage);
            resolved.Add((region, definition.Monster, count));
            totalRequested += count;
        }

        if (resolved.Count == 0 || !containsExpectedBoss ||
            state.Slots.Count + totalRequested > RegularMonsterTableCapacity)
            return false;

        var added = new List<MonsterSpawnSlot>(totalRequested);
        foreach (var (region, monster, count) in resolved)
            for (var slotIndex = 0; slotIndex < count; slotIndex++)
            {
                var slot = new MonsterSpawnSlot
                {
                    Region = region,
                    Monster = monster,
                    ServerIndex = state.NextServerIndex++,
                    IsZone175MissionMonster = true,
                    IsZone175WaveBoss = Zone175RewardTables.IsWaveBossSpecialType(monster.SpecialType)
                };
                state.Slots.Add(slot);
                state.SlotsByServerIndex.Add(slot.ServerIndex, slot);
                added.Add(slot);
            }

        foreach (var slot in added)
            if (!Spawn(zone, slot))
            {
                ClearZone175MissionStage(zone);
                return false;
            }

        return true;
    }

    public void ClearZone175MissionStage(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (!_stateByZone.TryGetValue(zone.MapId, out var state))
            return;

        for (var index = state.Slots.Count - 1; index >= 0; index--)
        {
            var slot = state.Slots[index];
            if (!slot.IsZone175MissionMonster)
                continue;

            zone.DespawnMonsterSilently(slot.ServerIndex);
            state.SlotsByServerIndex.Remove(slot.ServerIndex);
            state.Slots.RemoveAt(index);
        }
    }

    private void ApplySummonStateReset(Zone zone, MonsterZoneSpawnState state)
    {
        var freedPrefix = Math.Min(RegionRowCountFor(zone.MapId), state.Slots.Count);
        for (var index = 0; index < freedPrefix; index++)
        {
            var slot = state.Slots[index];
            if (!slot.Alive)
                continue;

            zone.DespawnMonsterSilently(slot.ServerIndex);
            slot.Alive = false;
            slot.CorpsePending = false;
            slot.RespawnDueAtZoneClock = null;
            slot.RespawnTicksRemaining = 0;
        }

        Interlocked.Exchange(ref state.IgnoreRespawnDelayPending, 1);
    }

    private int RegionRowCountFor(short mapId)
    {
        return worldData.ZonesByNumber.TryGetValue(mapId, out var zoneDef) ? zoneDef.MonsterSpawnRegions.Length : 0;
    }

    public int SlotCountFor(short mapId)
    {
        return _stateByZone.TryGetValue(mapId, out var state) ? state.Slots.Count : 0;
    }

    public void AnnounceStoneFirstAttack(int eventCode, int slotIndex, byte attackerTribe, string attackerName)
    {
        if (siegeIngestor is null)
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, slotIndex);
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..], attackerTribe);

        LegacyWireCodec.WriteFixedString(payload.Slice(FirstAttackNameOffset, FirstAttackNameSize), attackerName);

        siegeIngestor.Value.Ingest(eventCode, payload);
    }

    private MonsterZoneSpawnState BuildState(short mapId, bool isDungeonZone)
    {
        var snapshot = worldData.Capture();
        var regions = snapshot.ZonesByNumber.TryGetValue(mapId, out var zoneDef)
            ? zoneDef.MonsterSpawnRegions
            : [];

        var resolved = new List<(MonsterSpawnRegionRowDto Region, MonsterRowDto Monster, int SpawnCount)>();
        var totalRequested = 0;
        foreach (var region in regions)
        {
            if (region.MonsterId is not { } monsterId ||
                !snapshot.MonstersById.TryGetValue(monsterId, out var monsterDefinition))
                continue;

            var spawnCount = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(isDungeonZone,
                Math.Max(0, region.Number), monsterDefinition.Monster.MonsterId);
            resolved.Add((region, monsterDefinition.Monster, spawnCount));
            totalRequested += spawnCount;
        }

        var random = _randomFactory();
        var slots = new List<MonsterSpawnSlot>();
        var nextServerIndex = 0;
        var now = DateTime.UtcNow;

        var capacity = DungeonSpawnDensityPolicy.ResolveTableCapacity(isDungeonZone, RegularMonsterTableCapacity);
        if (totalRequested <= capacity)
            foreach (var (region, monster, slotCount) in resolved)
                for (var i = 0; i < slotCount; i++)
                {
                    var slot = new MonsterSpawnSlot
                    {
                        Region = region,
                        Monster = monster,
                        ServerIndex = nextServerIndex++
                    };

                    if (IsPersistedBossMonster(mapId, monster.MonsterId) && bossRespawnTracker is { } tracker &&
                        tracker.TryGetNextSpawnUtc(region.MonsterSpawnRegionId, out var dueAtUtc))
                    {
                        slot.HasPersistedDeadline = true;
                        slot.RespawnTicksRemaining = SimulationClock.ToWholeLegacyTicks(dueAtUtc - now);
                    }
                    else
                    {
                        slot.RespawnTicksRemaining = RollUniformRespawnTicks(monster, random);
                    }

                    slots.Add(slot);
                }

        var slotsByServerIndex = new Dictionary<int, MonsterSpawnSlot>(slots.Count);
        foreach (var slot in slots)
            slotsByServerIndex[slot.ServerIndex] = slot;

        return new MonsterZoneSpawnState
        {
            Slots = slots,
            SlotsByServerIndex = slotsByServerIndex,
            DropRoller = new MonsterDropRoller(snapshot, random),
            Random = random,
            NextServerIndex = nextServerIndex
        };
    }

    private void RefreshSlotDefinitions(short mapId, MonsterZoneSpawnState state, long configurationRevision)
    {
        var snapshot = worldData.Capture();
        if (!snapshot.ZonesByNumber.TryGetValue(mapId, out var zoneDefinition))
        {
            state.AppliedConfigurationRevision = configurationRevision;
            return;
        }

        var regionsById = new Dictionary<int, MonsterSpawnRegionRowDto>(zoneDefinition.MonsterSpawnRegions.Length);
        foreach (var region in zoneDefinition.MonsterSpawnRegions)
            regionsById.TryAdd(region.MonsterSpawnRegionId, region);
        foreach (var slot in state.Slots)
        {
            if (!regionsById.TryGetValue(slot.Region.MonsterSpawnRegionId, out var region) ||
                region.MonsterId is not { } monsterId ||
                !snapshot.MonstersById.TryGetValue(monsterId, out var monsterDefinition))
                continue;

            slot.Region = region;
            slot.Monster = monsterDefinition.Monster;
        }

        state.AppliedConfigurationRevision = configurationRevision;
    }

    private bool Spawn(Zone zone, MonsterSpawnSlot slot)
    {
        var state = _stateByZone[zone.MapId];
        var region = slot.Region;
        var angle = state.Random.NextDouble() * (Math.PI * 2);
        var scatter = (float)(state.Random.NextDouble() * Math.Max(0, region.Radius));
        var x = region.LocationX + (float)(Math.Cos(angle) * scatter);
        var z = region.LocationZ + (float)(Math.Sin(angle) * scatter);
        var y = (float)region.LocationY;

        if (!zone.Geometry.TryGetGroundHeight(x, z, out var groundY))
            return false;
        y = groundY;

        var entity = MonsterEntity.Create(slot.ServerIndex, zone.NextMonsterUniqueNumber(), slot.Monster,
            slot.ServerIndex, x, y, z, random: new RandomSourceAdapter(state.Random));

        zone.SpawnMonster(entity);
        slot.Alive = true;
        slot.CorpsePending = false;
        slot.RespawnDueAtZoneClock = null;
        slot.RespawnTicksRemaining = 0;

        if (IsPersistedBossMonster(zone.MapId, slot.Monster.MonsterId) && bossRespawnTracker is { } tracker)
            tracker.SetNextSpawnUtc(slot.Region.MonsterSpawnRegionId, YangGokNormalBossAliveSentinelUtc);

        return true;
    }

    private void DrainDeaths(Zone zone, MonsterZoneSpawnState state)
    {
        while (zone.TryDequeueDeadMonster(out var death))
            ProcessDeath(zone, state, death!);
    }

    private void DrainInvalidations(Zone zone, MonsterZoneSpawnState state)
    {
        while (zone.TryDequeueInvalidatedMonster(out var monster))
        {
            if (state.SlotsByServerIndex.TryGetValue(monster!.ServerIndex, out var slot))
            {
                slot.Alive = false;
                slot.CorpsePending = false;
                if (slot.IsZone175WaveBoss)
                {
                    slot.RespawnTicksRemaining = int.MaxValue;
                    slot.RespawnDueAtZoneClock = null;
                    continue;
                }
            }

            if (monster!.SpecialSort == MonsterSpecialSort.TribeSymbolStone &&
                TribeSymbolIndexOf(monster.Template.SpecialType) is { } symbolIndex &&
                monster.TryResolveTribeSymbolWinner(out var winnerTribe))
                zoneEventBroadcaster?.Value.AnnounceSymbolResolved(symbolIndex, winnerTribe);

            if (monster.SpecialSort == MonsterSpecialSort.AllianceStone &&
                AllianceStoneTribeOf(monster.Template.SpecialType) is { } stoneTribe)
                siegeIngestor?.Value.AnnounceAllianceStoneDestroyed(stoneTribe, monster.LastAttackerTribe,
                    monster.LastAttackerName);
        }
    }

    private static int RollRespawnTicks(MonsterRowDto monster, Random random)
    {
        return monster.MonsterId == 746
            ? SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(240))
            : RollUniformRespawnTicks(monster, random);
    }

    private static int RollUniformRespawnTicks(MonsterRowDto monster, Random random)
    {
        var minSeconds = monster.SummonTime1;
        var maxSeconds = monster.SummonTime2;
        var seconds = maxSeconds > minSeconds ? minSeconds + random.Next(maxSeconds - minSeconds + 1) : minSeconds;
        return SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(Math.Max(0, seconds)));
    }

    private static bool IsPersistedBossMonster(short mapId, int monsterId)
    {
        return mapId == YangGokNormalBossZoneId && monsterId is >= 564 and <= 568;
    }

    private static bool IsRespawnDue(MonsterSpawnSlot slot, TimeSpan zoneClock)
    {
        return slot.RespawnDueAtZoneClock is { } dueAt ? zoneClock >= dueAt : slot.RespawnTicksRemaining <= 0;
    }

    private void ArmRespawnDeadline(Zone zone, MonsterZoneSpawnState state, DeadMonsterEvent death)
    {
        if (!state.SlotsByServerIndex.TryGetValue(death.Monster.ServerIndex, out var slot))
            return;

        slot.CorpsePending = true;
        if (slot.IsZone175WaveBoss)
        {
            slot.RespawnTicksRemaining = int.MaxValue;
            slot.RespawnDueAtZoneClock = null;
            return;
        }

        var respawnTicks = RollRespawnTicks(slot.Monster, state.Random);
        var respawnDelay = SimulationClock.ToTimeSpan(respawnTicks);
        slot.RespawnTicksRemaining = respawnTicks;
        slot.RespawnDueAtZoneClock = death.DiedAtZoneClock + respawnDelay;

        if (IsPersistedBossMonster(zone.MapId, slot.Monster.MonsterId) && bossRespawnTracker is { } tracker)
            tracker.SetNextSpawnUtc(slot.Region.MonsterSpawnRegionId, death.DiedAtUtc + respawnDelay);
    }

    private void ProcessDeath(Zone zone, MonsterZoneSpawnState state, DeadMonsterEvent death)
    {
        var monster = death.Monster;
        ArmRespawnDeadline(zone, state, death);

        PlayerRuntimeState? attacker = null;
        if (death.AttackerCharacterId is { } attackerId)
            zone.TryGetPlayer(attackerId, out attacker);

        if (attacker is not null)
        {
            zone.ApplyQuestKillProgress(attacker.CharacterId, monster.Template.MonsterId,
                partyRegistry?.GetMembers(attacker.CharacterId));

            ApplyTowerCpForPvmMilestone(zone, attacker, monster.Template.RealLevel);

            if (monster.SpecialSort == MonsterSpecialSort.Tower)
                ApplyTowerDrop(zone, state, monster);
        }

        PlayerRuntimeState? creditedAvatar = null;
        if (death.CreditedCharacterId is { } creditedId)
            zone.TryGetPlayer(creditedId, out creditedAvatar);

        IReadOnlyList<int>? partyMemberIds = null;
        var killRaceInterceptsExperienceGrant = false;
        if (creditedAvatar is not null &&
            worldData.MonstersById.TryGetValue(monster.Template.MonsterId, out var monsterDefinition))
            partyMemberIds = GrantKillLoot(zone, state, monster, monsterDefinition, creditedAvatar,
                out killRaceInterceptsExperienceGrant);

        zone.BroadcastMonsterDeath(monster);

        if (creditedAvatar is not null && !killRaceInterceptsExperienceGrant)
            zone.GrantMonsterKillExperience(creditedAvatar.CharacterId, monster.Template.RealLevel,
                monster.Template.GeneralExperience, partyMemberIds,
                monster.Template.PatExperience, monster.Template.Life, monster.Template.ItemLevel);
    }

    private void ApplyTowerDrop(Zone zone, MonsterZoneSpawnState state, MonsterEntity monster)
    {
        if (towerWar is null)
            return;

        var towerIndex = TowerZoneIndexTable.GetTowerIndex(zone.MapId);
        if (towerIndex < 0)
            return;

        var stateCode = TowerWarState.DecodeLevel(towerWar.GetPackedState(towerIndex));
        var (itemId, bonusRate) = stateCode switch
        {
            2 => (TowerDropCpGiftCard5ItemId, 5),
            4 => (TowerDropCpGiftCard10ItemId, 10),
            6 => (TowerDropCpGiftCard15ItemId, 15),
            8 => (TowerDropCpGiftCard20ItemId, 20),
            _ => (0, 0)
        };

        if (itemId == 0)
            return;

        if (state.Random.Next(100) < bonusRate)
            TrySpawnMonsterDrop(zone, new DroppedItem(TowerDropBonusItemId, 1), monster.PosX, monster.PosY,
                monster.PosZ, SystemDropMasterName, "", 0, monster.InstanceId);

        for (var i = 0; i < TowerDropQuantity; i++)
            TrySpawnMonsterDrop(zone, new DroppedItem(itemId, 1), monster.PosX, monster.PosY, monster.PosZ,
                SystemDropMasterName, "", 0, monster.InstanceId);
    }

    private IReadOnlyList<int>? GrantKillLoot(Zone zone, MonsterZoneSpawnState state, MonsterEntity monster,
        MonsterDefinition monsterDefinition, PlayerRuntimeState creditedAvatar,
        out bool killRaceInterceptsExperienceGrant)
    {
        killRaceInterceptsExperienceGrant =
            valleyWarKillRegistry?.RegisterMonsterKill(zone.MapId, creditedAvatar.Tribe) ?? false;

        if (zone.IsZone200TypeZone)
            return null;

        var dropEligible = MonsterDropRoller.IsEligible(monsterDefinition.Monster, creditedAvatar.Level,
            creditedAvatar.Level2);
        zone.NotifyPopupEventMonsterKill(creditedAvatar, dropEligible);

        var partyMemberIds = partyRegistry?.GetMembers(creditedAvatar.CharacterId);
        var partyName = partyMemberIds is { Count: > 0 } && partyRegistry is not null
            ? PartyIdentityResolver.ResolveCurrentPartyName(partyRegistry, creditedAvatar.CharacterId,
                creditedAvatar.Name,
                memberId => zone.TryGetPlayer(memberId, out var member) ? member?.Name : null)
            : "";

        bool CreditedAvatarHasItem(int itemId)
        {
            foreach (var entry in creditedAvatar.Inventory.GetContainer(ContainerMatrix.InventoryPage0))
                if (entry.Value.ItemId == itemId)
                    return true;

            foreach (var entry in creditedAvatar.Inventory.GetContainer(ContainerMatrix.InventoryPage1))
                if (entry.Value.ItemId == itemId)
                    return true;

            return false;
        }

        var creditedAvatarQuest = new QuestProgress(creditedAvatar.QuestStepPermanent, creditedAvatar.QuestActiveFlag,
            creditedAvatar.QuestSort, creditedAvatar.QuestTargetPhase, creditedAvatar.QuestKillCounter);

        var luck = (creditedAvatar.Stats?.Luck ?? 0) * 10;

        var demonLordKillTally = monster.Template.MonsterId == BossEventDropResolver.DemonLordMonsterId
            ? ++state.DemonLordKillTally
            : 0;
        var bossOutcome = BossEventDropResolver.Resolve(monster.Template.MonsterId, demonLordKillTally, state.Random,
            _bossDropCatalog);

        ApplyBossDropSideEffects(zone, creditedAvatar, bossOutcome);

        long? money;
        IReadOnlyList<DroppedItem> genericItems;
        if (bossOutcome.SkipGenericTiers)
        {
            money = null;
            genericItems = [];
        }
        else
        {
            var tribeItemDropBonus = siegeState?.GetItemDropBonusRatio(creditedAvatar.Tribe) ?? 0f;
            var tribeRareDropBonus = siegeState?.GetMyoungItemDropBonusRatio(creditedAvatar.Tribe) ?? 0f;
            var itemDropUpBuffActive = creditedAvatar.StateTimeEffect == ItemDropUpStateTimeEffect;

            var result = state.DropRoller.Roll(monsterDefinition, creditedAvatar.Level,
                creditedAvatar.PreviousTribe, luck, creditedAvatarQuest, CreditedAvatarHasItem,
                creditedAvatar.PremiumExpireUtc > 0,
                creditedAvatar.Level2,
                creditedAvatar.DropItemTime > 0,
                zone.IsZone039TypeZone,
                itemDropUpBuffActive,
                tribeItemDropBonus,
                tribeRareDropBonus);

            money = result.Money;
            genericItems = result.Items;
        }

        if (money is { } amount)
            zone.TryGrantMonsterMoney(creditedAvatar, amount);

        var (dropX, dropY, dropZ) = ResolveKillDropLocation(monster, creditedAvatar);

        foreach (var publicItem in bossOutcome.PublicItems)
            TrySpawnMonsterDrop(zone, publicItem, dropX, dropY, dropZ, "", "", 0, monster.InstanceId);

        if (bossOutcome.Items.Count > 0 || genericItems.Count > 0)
        {
            var dropSort = ResolveMonsterDropSort(partyMemberIds);

            foreach (var item in bossOutcome.Items)
                TrySpawnMonsterDrop(zone, item, dropX, dropY, dropZ, creditedAvatar.Name, partyName, dropSort,
                    monster.InstanceId);

            foreach (var item in genericItems)
                TrySpawnMonsterDrop(zone, item, dropX, dropY, dropZ, creditedAvatar.Name, partyName,
                    dropSort, monster.InstanceId);
        }

        return partyMemberIds;
    }

    private static (float X, float Y, float Z) ResolveKillDropLocation(MonsterEntity monster,
        PlayerRuntimeState creditedAvatar)
    {
        return monster.Template.MonsterId < KillerLocationDropMonsterIdCeiling
            ? (creditedAvatar.PosX, creditedAvatar.PosY, creditedAvatar.PosZ)
            : (monster.PosX, monster.PosY, monster.PosZ);
    }

    private static void ApplyBossDropSideEffects(Zone zone, PlayerRuntimeState creditedAvatar, BossDropOutcome outcome)
    {
        if (outcome.ContributionPointsGranted != 0)
            zone.GrantContributionPoints(creditedAvatar.CharacterId, outcome.ContributionPointsGranted);

        if (outcome.WarPointsGranted != 0)
            zone.GrantWarPoints(creditedAvatar.CharacterId, outcome.WarPointsGranted);

        if (outcome.BloodPointsGranted != 0)
            zone.GrantBloodPoints(creditedAvatar.CharacterId, outcome.BloodPointsGranted);

        if (outcome.AnnounceEliteBossDefeat)
            zone.AnnounceEliteBossDefeated(creditedAvatar.Tribe, creditedAvatar.Name);
    }

    private static int ResolveMonsterDropSort(IReadOnlyList<int>? partyMemberIds)
    {
        return partyMemberIds is { Count: > 0 } ? GroundItemEntity.MonsterKillDropSort : 0;
    }

    private void TrySpawnMonsterDrop(Zone zone, in DroppedItem item, float dropX, float dropY, float dropZ,
        string ownerName, string partyName, int dropSort, int? instanceId)
    {
        if (!worldData.ItemsById.TryGetValue(item.ItemId, out var definition))
        {
            zone.SpawnGroundItem(item.ItemId, item.Quantity, dropX, dropY, dropZ, ownerName, partyName, dropSort,
                instanceId);
            return;
        }

        var itemType = definition.Item.Type;
        if (itemType is not (ItemSerialGenerator.UniqueItemType or ItemSerialGenerator.RareItemType or
            ItemSerialGenerator.EliteItemType))
        {
            zone.SpawnGroundItem(item.ItemId, item.Quantity, dropX, dropY, dropZ, ownerName, partyName, dropSort,
                instanceId);
            return;
        }

        var request = new GroundItemDropRequest(
            new GroundItemReference(item.ItemId, itemType),
            item.Quantity,
            GroundItemOrigin.Monster,
            new GroundItemReplicationState(
                new GroundItemState(0, 0, 0, 0),
                new GroundItemSocketGems(0, 0, 0),
                0));
        var result = groundItemFactory.Create(in request);
        if (result.Drop is not { } drop)
            return;

        var plan = new GroundItemSpawnPlan(
            drop.Item.ItemId,
            drop.Quantity,
            drop.Replication.PackedValue,
            drop.SerialNumber,
            drop.Replication.SocketGems.First,
            drop.Replication.SocketGems.Second,
            drop.Replication.SocketGems.Third,
            dropX,
            dropY,
            dropZ,
            ownerName,
            partyName,
            dropSort);
        zone.SpawnGroundItem(in plan, instanceId);
    }

    private void ApplyTowerCpForPvmMilestone(Zone zone, PlayerRuntimeState attacker, int monsterRealLevel)
    {
        var registration = TowerCpForPvmMilestone.RegisterKill(attacker.TowerCpMilestoneCounter, attacker.Level,
            attacker.Level2, monsterRealLevel);
        attacker.TowerCpMilestoneCounter = registration.UpdatedCounter;

        if (!registration.MilestoneReached)
            return;

        var towerBonus = towerWar?.GetTribeBonus(attacker.Tribe).CpForPvmBonus ?? 0;
        zone.GrantContributionPoints(attacker.CharacterId, TowerCpForPvmMilestone.ComputeReward(towerBonus));
    }

    private static byte? TribeSymbolIndexOf(byte specialType)
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

    private static byte? AllianceStoneTribeOf(byte specialType)
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
}
