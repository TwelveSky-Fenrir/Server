using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Wire;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.Stats;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PopupEventRewardSystem(
    PopupEventState state,
    WorldDataCache worldData,
    Lazy<ZoneCenterBroadcastIngestor>? centerIngestor = null) : ISimulationSystem
{
    private const int CombinedLevelGapCap = 13;

    private const int WarPointRewardAmount = 1;

    private const int WarPopupKillCounterStatSort = 402;

    private const int PopupKillCounterStatSort = 401;

    private const int PopupRewardDropSort = 11;

    private const int PopupRewardNoticeType = 57;

    private static readonly int[] MonsterPrimaryRewards = [506, 507, 508, 509, 578, 579, 1023, 1041, 93500, 93501];

    private static readonly int[] PvpPrimaryRewards =
        [506, 507, 508, 509, 578, 579, 1103, 2397, 1023, 1041, 93500, 93501, 1166, 8103, 1237];

    private static readonly int[] RegularWarTertiaryOne =
        [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326, 1002, 1003, 1004, 1005, 1407];

    private static readonly int[] RegularWarTertiaryTwo = [8420, 8105, 8405, 8109];

    private readonly ConditionalWeakTable<PlayerRuntimeState, PopupCounters> _counters = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
    }

    public void NotifyPvpKill(Zone zone, PlayerRuntimeState killer, PlayerRuntimeState victim)
    {
        if (killer.CharacterId == victim.CharacterId)
            return;

        if (killer.CombinedLevel - victim.CombinedLevel > CombinedLevelGapCap)
            return;

        if (!PopupEventZoneCatalog.TryResolvePvpType(zone.MapId, out var type))
            return;

        if (type == PopupEventType.YanggokPvp && PvpKillCreditGuard.IsSameOrigin(killer.SourceIp, victim.SourceIp))
            return;

        if (!state.IsEnabled(type))
            return;

        if (type is PopupEventType.YanggokPvp or PopupEventType.InvasionPvp &&
            victim.Level != LevelProgressionCalculator.MaxLevel)
            return;

        var counters = _counters.GetValue(killer, static _ => new PopupCounters());
        var threshold = PopupEventZoneCatalog.KillThreshold(type, zone.MapId);

        if (PopupEventZoneCatalog.UsesWarCounter(type))
        {
            counters.War = Math.Min(counters.War + 1, threshold);
            SendWarPopupCounter(killer, counters.War);
            if (counters.War < threshold)
                return;

            DeliverReward(zone, type, killer);
            counters.War = 0;
            SendWarPopupCounter(killer, counters.War);
            return;
        }

        counters.Avt = Math.Min(counters.Avt + 1, threshold);
        SendPopupKillCounters(killer, counters);
        if (counters.Avt < threshold)
            return;

        DeliverReward(zone, type, killer);
        counters.Avt = 0;
        SendPopupKillCounters(killer, counters);
    }

    public void NotifyMonsterKill(Zone zone, PlayerRuntimeState killer, bool dropEligible)
    {
        if (!dropEligible || !PopupEventZoneCatalog.IsMonsterPopupMap(zone.MapId) ||
            !state.IsEnabled(PopupEventType.MonsterPve))
            return;

        var counters = _counters.GetValue(killer, static _ => new PopupCounters());
        counters.Monster = Math.Min(counters.Monster + 1, PopupEventZoneCatalog.MonsterKillThreshold);
        SendPopupKillCounters(killer, counters);
        if (counters.Monster < PopupEventZoneCatalog.MonsterKillThreshold)
            return;

        DeliverReward(zone, PopupEventType.MonsterPve, killer);
        counters.Avt = 0;
        counters.Monster = 0;
        SendPopupKillCounters(killer, counters);
    }

    private static void SendPopupKillCounters(PlayerRuntimeState killer, PopupCounters counters)
    {
        killer.Session.Send(new AvatarStatUpdateResponse
            { Sort = PopupKillCounterStatSort, Value = counters.Avt, Value2 = counters.Monster });
    }

    private static void SendWarPopupCounter(PlayerRuntimeState killer, int counter)
    {
        killer.Session.Send(new AvatarStatUpdateResponse
            { Sort = WarPopupKillCounterStatSort, Value = counter, Value2 = 0 });
    }

    private void DeliverReward(Zone zone, PopupEventType type, PlayerRuntimeState killer)
    {
        zone.GrantWarPoints(killer.CharacterId, WarPointRewardAmount);

        var primary = type == PopupEventType.MonsterPve ? MonsterPrimaryRewards : PvpPrimaryRewards;
        SpawnAndAnnounce(zone, killer, primary[SystemRandomSource.Instance.NextInt32(primary.Length)]);

        if (TrySelectSecondaryReward(type, killer.PreviousTribe, out var secondaryItemId))
            SpawnAndAnnounce(zone, killer, secondaryItemId);

        if (type == PopupEventType.RegularWar && TrySelectRegularWarTertiaryReward(out var tertiaryItemId))
            SpawnAndAnnounce(zone, killer, tertiaryItemId);
    }

    private static bool TrySelectSecondaryReward(PopupEventType type, byte previousTribe, out int itemId)
    {
        var roll = type switch
        {
            PopupEventType.YanggokPvp => SystemRandomSource.Instance.NextInt32(6000),
            PopupEventType.MonsterPve => SystemRandomSource.Instance.NextInt32(16000),
            _ => 0
        };

        ReadOnlySpan<int> candidates = roll switch
        {
            < 2 => previousTribe switch
            {
                0 => [15157],
                1 => [35135],
                2 => [55157],
                _ => []
            },
            < 3 => previousTribe switch
            {
                0 => [15267],
                1 => [35267],
                2 => [55267],
                _ => []
            },
            < 5 => previousTribe switch
            {
                0 => [15201],
                1 => [35201],
                2 => [55201],
                _ => []
            },
            < 10 => previousTribe switch
            {
                0 => [15135, 15179, 15223, 15245, 15289],
                1 => [35157, 35179, 35223, 35245, 35289],
                2 => [55135, 55179, 55223, 55245, 55289],
                _ => []
            },
            _ => []
        };

        if (candidates.IsEmpty)
        {
            itemId = 0;
            return false;
        }

        itemId = candidates[SystemRandomSource.Instance.NextInt32(candidates.Length)];
        return true;
    }

    private static bool TrySelectRegularWarTertiaryReward(out int itemId)
    {
        var roll = SystemRandomSource.Instance.NextInt32(100);
        if (roll <= 15)
        {
            itemId = RegularWarTertiaryOne[SystemRandomSource.Instance.NextInt32(RegularWarTertiaryOne.Length)];
            return true;
        }

        if (roll <= 20)
        {
            itemId = RegularWarTertiaryTwo[SystemRandomSource.Instance.NextInt32(RegularWarTertiaryTwo.Length)];
            return true;
        }

        itemId = 0;
        return false;
    }

    private void SpawnAndAnnounce(Zone zone, PlayerRuntimeState killer, int itemId)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
            return;

        var serial = ItemSerialGenerator.Generate(itemDefinition.Item.Type, DateTimeOffset.Now);
        zone.SpawnGroundItem(new GroundItemSpawnPlan(itemId, 1, 0, serial, 0, 0, 0, killer.PosX, killer.PosY,
            killer.PosZ, killer.Name, "", PopupRewardDropSort), killer.DungeonInstanceId);

        if (centerIngestor is null)
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, PopupRewardNoticeType);
        BinaryPrimitives.WriteInt32LittleEndian(payload[sizeof(int)..], killer.Tribe);
        BinaryPrimitives.WriteInt32LittleEndian(payload[(sizeof(int) * 2)..], itemId);
        LegacyWireCodec.WriteFixedString(payload.Slice(sizeof(int) * 4, 13), killer.Name);
        centerIngestor.Value.Ingest(2000, payload);
    }

    private sealed class PopupCounters
    {
        public int Avt;

        public int Monster;

        public int War;
    }
}
