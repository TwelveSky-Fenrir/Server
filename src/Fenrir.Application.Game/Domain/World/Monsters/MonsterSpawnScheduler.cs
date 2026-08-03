using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Monsters;

internal sealed class MonsterSpawnSlot
{
    public required MonsterSpawnRegionRowDto Region { get; init; }
    public required MonsterRowDto Monster { get; init; }
    public required int ServerIndex { get; init; }
    public bool Alive { get; set; }
    public int RespawnTicksRemaining { get; set; }
    public bool HasPersistedDeadline { get; set; }
}

internal sealed class MonsterZoneSpawnState
{
    public int IgnoreRespawnDelayPending;

    public int SummonStateResetPending;
    public required List<MonsterSpawnSlot> Slots { get; init; }
    public required MonsterDropRoller DropRoller { get; init; }
    public required Random Random { get; init; }
    public bool InitialPopDone { get; set; }
    public int TicksSinceLastScan { get; set; }
}

public sealed class MonsterSpawnScheduler(
    WorldDataCache worldData,
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

    private static readonly DateTime YangGokNormalBossAliveSentinelUtc = DateTime.MinValue;

    private readonly BossDropCatalog _bossDropCatalog = bossDropCatalog ?? BossDropCatalog.Default;

    private readonly Func<Random> _randomFactory = randomFactory ?? (static () => new Random());

    private readonly ConcurrentDictionary<short, MonsterZoneSpawnState> _stateByZone = new();

    private int _demonLordKillTally;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone.MapId, zone.IsDungeonServerZone));

        if (!state.InitialPopDone)
        {
            state.InitialPopDone = true;
            foreach (var slot in state.Slots)
                if (!slot.Alive && (!slot.HasPersistedDeadline || slot.RespawnTicksRemaining <= 0))
                    Spawn(zone, slot);
        }

        DrainDeaths(zone, state);
        DrainInvalidations(zone, state);

        if (Interlocked.Exchange(ref state.SummonStateResetPending, 0) != 0)
            ApplySummonStateReset(zone, state);

        foreach (var slot in state.Slots)
            if (!slot.Alive)
                slot.RespawnTicksRemaining = Math.Max(0, slot.RespawnTicksRemaining - legacyTicksElapsed);

        state.TicksSinceLastScan += legacyTicksElapsed;
        if (state.TicksSinceLastScan < SimulationClock.MonsterRespawnScanLegacyTicks)
            return;

        state.TicksSinceLastScan = 0;
        var ignoreRespawnDelay = Interlocked.Exchange(ref state.IgnoreRespawnDelayPending, 0) != 0;
        foreach (var slot in state.Slots)
            if (!slot.Alive && (ignoreRespawnDelay || slot.RespawnTicksRemaining <= 0))
                Spawn(zone, slot);
    }

    public void RequestSummonStateReset(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone.MapId, zone.IsDungeonServerZone));
        Interlocked.Exchange(ref state.SummonStateResetPending, 1);
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

        var nameField = payload.Slice(FirstAttackNameOffset, FirstAttackNameSize);
        var copied = Math.Min(attackerName.Length, FirstAttackNameSize - 1);
        Encoding.ASCII.GetBytes(attackerName.AsSpan(0, copied), nameField);

        siegeIngestor.Value.Ingest(eventCode, payload);
    }

    private MonsterZoneSpawnState BuildState(short mapId, bool isDungeonZone)
    {
        var regions = worldData.ZonesByNumber.TryGetValue(mapId, out var zoneDef)
            ? zoneDef.MonsterSpawnRegions
            : [];

        var resolved = new List<(MonsterSpawnRegionRowDto Region, MonsterRowDto Monster, int SpawnCount)>();
        var totalRequested = 0;
        foreach (var region in regions)
        {
            if (region.MonsterId is not { } monsterId ||
                !worldData.MonstersById.TryGetValue(monsterId, out var monsterDefinition))
                continue;

            var spawnCount = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(isDungeonZone,
                Math.Max(0, region.Number), monsterDefinition.Monster.MonsterId);
            resolved.Add((region, monsterDefinition.Monster, spawnCount));
            totalRequested += spawnCount;
        }

        var random = _randomFactory();
        var slots = new List<MonsterSpawnSlot>();
        var nextServerIndex = 1;
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

        return new MonsterZoneSpawnState
        {
            Slots = slots,
            DropRoller = new MonsterDropRoller(worldData, random),
            Random = random
        };
    }

    private void Spawn(Zone zone, MonsterSpawnSlot slot)
    {
        var state = _stateByZone[zone.MapId];
        var region = slot.Region;
        var angle = state.Random.NextDouble() * (Math.PI * 2);
        var scatter = (float)(state.Random.NextDouble() * Math.Max(0, region.Radius));
        var x = region.LocationX + (float)(Math.Cos(angle) * scatter);
        var z = region.LocationZ + (float)(Math.Sin(angle) * scatter);
        var y = (float)region.LocationY;

        if (zone.Geometry is { } geometry)
        {
            if (!geometry.TryGetGroundHeight(x, z, out var groundY))
                return;
            y = groundY;
        }

        var entity = MonsterEntity.Create(slot.ServerIndex, zone.NextMonsterUniqueNumber(), slot.Monster,
            slot.ServerIndex, x, y, z);

        zone.SpawnMonster(entity);
        slot.Alive = true;

        if (IsPersistedBossMonster(zone.MapId, slot.Monster.MonsterId) && bossRespawnTracker is { } tracker)
            tracker.SetNextSpawnUtc(slot.Region.MonsterSpawnRegionId, YangGokNormalBossAliveSentinelUtc);
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
            var slot = state.Slots.Find(s => s.ServerIndex == monster!.ServerIndex);
            if (slot is not null)
            {
                slot.Alive = false;
                var respawnTicks = RollRespawnTicks(slot.Monster, state.Random);
                slot.RespawnTicksRemaining = respawnTicks;

                if (IsPersistedBossMonster(zone.MapId, slot.Monster.MonsterId) && bossRespawnTracker is { } tracker)
                    tracker.SetNextSpawnUtc(slot.Region.MonsterSpawnRegionId,
                        DateTime.UtcNow + SimulationClock.ToTimeSpan(respawnTicks));
            }

            if (monster!.SpecialSort == MonsterSpecialSort.TribeSymbolStone &&
                TribeSymbolIndexOf(monster.Template.SpecialType) is { } symbolIndex &&
                monster.TryResolveTribeSymbolWinner(out var winnerTribe))
                zoneEventBroadcaster?.Value.AnnounceSymbolResolved(symbolIndex, winnerTribe);
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

    private void ProcessDeath(Zone zone, MonsterZoneSpawnState state, DeadMonsterEvent death)
    {
        var monster = death.Monster;

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
            zone.SpawnGroundItem(TowerDropBonusItemId, 1, monster.PosX, monster.PosY, monster.PosZ,
                SystemDropMasterName, "", 0, monster.InstanceId);

        for (var i = 0; i < TowerDropQuantity; i++)
            zone.SpawnGroundItem(itemId, 1, monster.PosX, monster.PosY, monster.PosZ, SystemDropMasterName, "", 0,
                monster.InstanceId);
    }

    private IReadOnlyList<int>? GrantKillLoot(Zone zone, MonsterZoneSpawnState state, MonsterEntity monster,
        MonsterDefinition monsterDefinition, PlayerRuntimeState creditedAvatar,
        out bool killRaceInterceptsExperienceGrant)
    {
        var dropEligible = MonsterDropRoller.IsEligible(monsterDefinition.Monster, creditedAvatar.Level,
            creditedAvatar.Level2);
        zone.NotifyPopupEventMonsterKill(creditedAvatar, dropEligible);

        killRaceInterceptsExperienceGrant =
            valleyWarKillRegistry?.RegisterMonsterKill(zone.MapId, creditedAvatar.Tribe) ?? false;

        var partyMemberIds = partyRegistry?.GetMembers(creditedAvatar.CharacterId);

        bool CreditedAvatarHasItem(int itemId)
        {
            return creditedAvatar.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values
                       .Any(s => s.ItemId == itemId) ||
                   creditedAvatar.Inventory.GetContainer(ContainerMatrix.InventoryPage1).Values
                       .Any(s => s.ItemId == itemId);
        }

        var creditedAvatarQuest = new QuestProgress(creditedAvatar.QuestStepPermanent, creditedAvatar.QuestActiveFlag,
            creditedAvatar.QuestSort, creditedAvatar.QuestTargetPhase, creditedAvatar.QuestKillCounter);

        var luck = (creditedAvatar.Stats?.Luck ?? 0) * 10;

        var demonLordKillTally = monster.Template.MonsterId == BossEventDropResolver.DemonLordMonsterId
            ? Interlocked.Increment(ref _demonLordKillTally)
            : 0;
        var bossOutcome = BossEventDropResolver.Resolve(monster.Template.MonsterId, demonLordKillTally, state.Random,
            worldData, _bossDropCatalog);

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
                killerPremiumActive: creditedAvatar.PremiumExpireUtc > 0,
                killerLevel2: creditedAvatar.Level2,
                killerDropItemTimeActive: creditedAvatar.DropItemTime > 0,
                isZone039TypeShard: zone.IsZone039TypeZone,
                killerItemDropUpBuffActive: itemDropUpBuffActive,
                tribeItemDropBonus: tribeItemDropBonus,
                tribeRareDropBonus: tribeRareDropBonus);

            money = result.Money;
            genericItems = result.Items;
        }

        if (money is { } amount)
        {
            zone.QueueMoneyGrant(creditedAvatar.CharacterId, amount);

            var tribeTaxBase = amount - (long)(amount * InventoryToWorldDropPolicy.MonsterMoneyGroundReductionRatio);
            zone.CreditMonsterKillTribeTax(creditedAvatar.Tribe, tribeTaxBase);
        }

        foreach (var publicItem in bossOutcome.PublicItems)
            zone.SpawnGroundItem(publicItem.ItemId, publicItem.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                "", "", 0, monster.InstanceId);

        if (bossOutcome.Items.Count > 0 || genericItems.Count > 0)
        {
            var (partyName, dropSort) = ResolvePartyDrop(zone, creditedAvatar, partyMemberIds);

            foreach (var item in bossOutcome.Items.Concat(genericItems))
                zone.SpawnGroundItem(item.ItemId, item.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                    creditedAvatar.Name, partyName, dropSort, monster.InstanceId);
        }

        return partyMemberIds;
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

    private static (string PartyName, int DropSort) ResolvePartyDrop(Zone zone, PlayerRuntimeState creditedAvatar,
        IReadOnlyList<int>? partyMemberIds)
    {
        if (partyMemberIds is not { Count: > 0 } members)
            return ("", 0);

        var leaderId = members[0];
        if (leaderId == creditedAvatar.CharacterId)
            return (creditedAvatar.Name, 1);

        return zone.TryGetPlayer(leaderId, out var leader) && leader is not null
            ? (leader.Name, 1)
            : (creditedAvatar.Name, 1);
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
}
