using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Monsters;

internal sealed class MonsterSpawnSlot
{
    public required MonsterSpawnRegionRowDto Region { get; init; }
    public required MonsterRowDto Monster { get; init; }
    public required int ServerIndex { get; init; }
    public bool Alive { get; set; }
    public int RespawnTicksRemaining { get; set; }
}

internal sealed class MonsterZoneSpawnState
{
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
    BossDropCatalog? bossDropCatalog = null)
    : ISimulationSystem
{
    private const int RegularMonsterTableCapacity = 3400;

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
                if (slot.RespawnTicksRemaining <= 0)
                    Spawn(zone, slot);
        }

        DrainDeaths(zone, state);

        foreach (var slot in state.Slots)
            if (!slot.Alive)
                slot.RespawnTicksRemaining = Math.Max(0, slot.RespawnTicksRemaining - legacyTicksElapsed);

        state.TicksSinceLastScan += legacyTicksElapsed;
        if (state.TicksSinceLastScan < SimulationClock.MonsterRespawnScanLegacyTicks)
            return;

        state.TicksSinceLastScan = 0;
        foreach (var slot in state.Slots)
            if (slot is { Alive: false, RespawnTicksRemaining: <= 0 })
                Spawn(zone, slot);
    }

    public int SlotCountFor(short mapId)
    {
        return _stateByZone.TryGetValue(mapId, out var state) ? state.Slots.Count : 0;
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

                    if (IsPersistedBossMonster(monster.MonsterId) && bossRespawnTracker is { } tracker &&
                        tracker.TryGetNextSpawnUtc(region.MonsterSpawnRegionId, out var dueAtUtc))
                        slot.RespawnTicksRemaining = SimulationClock.ToWholeLegacyTicks(dueAtUtc - now);

                    slots.Add(slot);
                }

        var random = _randomFactory();
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

        var leash = MathF.Max(region.Radius, 1f);
        var entity = MonsterEntity.Create(slot.ServerIndex, zone.NextMonsterUniqueNumber(), slot.Monster,
            slot.ServerIndex, x, y, z, leash);

        zone.SpawnMonster(entity);
        slot.Alive = true;
    }

    private void DrainDeaths(Zone zone, MonsterZoneSpawnState state)
    {
        while (zone.TryDequeueDeadMonster(out var death))
        {
            zone.RemoveMonsterFromGrid(death!.Monster);

            var slot = state.Slots.Find(s => s.ServerIndex == death!.Monster.ServerIndex);
            if (slot is not null)
            {
                slot.Alive = false;
                var respawnTicks = RollRespawnTicks(slot.Monster, state.Random);
                slot.RespawnTicksRemaining = respawnTicks;

                if (IsPersistedBossMonster(slot.Monster.MonsterId) && bossRespawnTracker is { } tracker)
                    tracker.SetNextSpawnUtc(slot.Region.MonsterSpawnRegionId,
                        DateTime.UtcNow + SimulationClock.ToTimeSpan(respawnTicks));
            }

            ProcessDeath(zone, state, death!);
        }
    }

    private static int RollRespawnTicks(MonsterRowDto monster, Random random)
    {
        if (monster.MonsterId == 746)
            return SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(240));

        var minSeconds = monster.SummonTime1;
        var maxSeconds = monster.SummonTime2;
        var seconds = maxSeconds > minSeconds ? minSeconds + random.Next(maxSeconds - minSeconds + 1) : minSeconds;
        return SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(Math.Max(0, seconds)));
    }

    private static bool IsPersistedBossMonster(int monsterId)
    {
        return monsterId is >= 564 and <= 568;
    }

    private void ProcessDeath(Zone zone, MonsterZoneSpawnState state, DeadMonsterEvent death)
    {
        var monster = death.Monster;
        if (!worldData.MonstersById.TryGetValue(monster.Template.MonsterId, out var monsterDefinition))
            return;

        zone.BroadcastMonsterDeath(monster);

        PlayerRuntimeState? killer = null;
        if (death.KillerCharacterId is { } killerId)
            zone.TryGetPlayer(killerId, out killer);

        if (killer is not null && TribeSymbolIndexOf(monster.Template.SpecialType) is { } symbolIndex)
            zoneEventBroadcaster?.Value.AnnounceSymbolResolved(symbolIndex, killer.Tribe);

        if (killer is null)
            return;

        var dropEligible = MonsterDropRoller.IsEligible(monsterDefinition.Monster, killer.Level);
        zone.NotifyPopupEventMonsterKill(killer, dropEligible);

        valleyWarKillRegistry?.RegisterMonsterKill(zone.MapId, killer.Tribe);

        var partyMemberIds = partyRegistry?.GetMembers(killer.CharacterId);
        zone.GrantMonsterKillExperience(killer.CharacterId, monster.Template.RealLevel,
            monster.Template.GeneralExperience, partyMemberIds,
            monster.Template.PatExperience, monster.Template.Life);

        zone.ApplyQuestKillProgress(killer.CharacterId, monster.Template.MonsterId, partyMemberIds);

        ApplyTowerCpForPvmMilestone(zone, killer, monster.Template.RealLevel);

        bool KillerHasItem(int itemId)
        {
            return killer.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   killer.Inventory.GetContainer(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var killerQuest = new QuestProgress(killer.QuestStepPermanent, killer.QuestActiveFlag, killer.QuestSort,
            killer.QuestTargetPhase, killer.QuestKillCounter);

        var luck = (killer.Stats?.Luck ?? 0) * 10;

        var demonLordKillTally = monster.Template.MonsterId == BossEventDropResolver.DemonLordMonsterId
            ? Interlocked.Increment(ref _demonLordKillTally)
            : 0;
        var bossOutcome = BossEventDropResolver.Resolve(monster.Template.MonsterId, demonLordKillTally, state.Random,
            worldData, _bossDropCatalog);

        ApplyBossDropSideEffects(zone, killer, bossOutcome);

        long? money;
        IReadOnlyList<DroppedItem> genericItems;
        if (bossOutcome.SkipGenericTiers)
        {
            money = null;
            genericItems = [];
        }
        else
        {
            var result = state.DropRoller.Roll(monsterDefinition, killer.Level, killer.Tribe, luck, killerQuest,
                KillerHasItem, killer.PremiumExpireUtc > 0);
            money = result.Money;

            var cpGiftItems = MonsterDropTailResolver.ResolveCpGiftCard(dropEligible,
                monster.Template.MonsterId, zone.IsZone241TypeZone, killer.Level2,
                zone.IsZone126TypeZone, state.Random);
            var rebirthItems = MonsterDropTailResolver.ResolveRebirthItem(monster.Template.MonsterId, state.Random);

            genericItems = cpGiftItems.Count == 0 && rebirthItems.Count == 0
                ? result.Items
                : [.. result.Items, .. cpGiftItems, .. rebirthItems];
        }

        if (money is { } amount)
            zone.QueueMoneyGrant(killer.CharacterId, amount);

        foreach (var publicItem in bossOutcome.PublicItems)
            zone.SpawnGroundItem(publicItem.ItemId, publicItem.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                "", "", 0, monster.InstanceId);

        if (bossOutcome.Items.Count == 0 && genericItems.Count == 0)
            return;

        var (partyName, dropSort) = ResolvePartyDrop(zone, killer, partyMemberIds);

        foreach (var item in bossOutcome.Items.Concat(genericItems))
            zone.SpawnGroundItem(item.ItemId, item.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                killer.Name, partyName, dropSort, monster.InstanceId);
    }

    private static void ApplyBossDropSideEffects(Zone zone, PlayerRuntimeState killer, BossDropOutcome outcome)
    {
        if (outcome.ContributionPointsGranted != 0)
            zone.GrantContributionPoints(killer.CharacterId, outcome.ContributionPointsGranted);

        if (outcome.WarPointsGranted != 0)
            zone.GrantWarPoints(killer.CharacterId, outcome.WarPointsGranted);

        if (outcome.BloodPointsGranted != 0)
            zone.GrantBloodPoints(killer.CharacterId, outcome.BloodPointsGranted);

        if (outcome.AnnounceEliteBossDefeat)
            zone.AnnounceEliteBossDefeated(killer.Tribe, killer.Name);
    }

    private static (string PartyName, int DropSort) ResolvePartyDrop(Zone zone, PlayerRuntimeState killer,
        IReadOnlyList<int>? partyMemberIds)
    {
        if (partyMemberIds is not { Count: > 0 } members)
            return ("", 0);

        var leaderId = members[0];
        if (leaderId == killer.CharacterId)
            return (killer.Name, 1);

        return zone.TryGetPlayer(leaderId, out var leader) && leader is not null
            ? (leader.Name, 1)
            : (killer.Name, 1);
    }

    private void ApplyTowerCpForPvmMilestone(Zone zone, PlayerRuntimeState killer, int monsterRealLevel)
    {
        var registration = TowerCpForPvmMilestone.RegisterKill(killer.TowerCpMilestoneCounter, killer.Level,
            killer.Level2, monsterRealLevel);
        killer.TowerCpMilestoneCounter = registration.UpdatedCounter;

        if (!registration.MilestoneReached)
            return;

        var towerBonus = towerWar?.GetTribeBonus(killer.Tribe).CpForPvmBonus ?? 0;
        zone.GrantContributionPoints(killer.CharacterId, TowerCpForPvmMilestone.ComputeReward(towerBonus));
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
