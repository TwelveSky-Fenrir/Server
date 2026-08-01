using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.Stats;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PopupEventRewardSystem(PopupEventState state) : ISimulationSystem
{
    private const int CombinedLevelGapCap = 13;

    private const int WarPointRewardAmount = 1;

    private const int WarPopupKillCounterStatSort = 402;

    private const int PopupKillCounterStatSort = 401;

    private readonly ConditionalWeakTable<PlayerRuntimeState, PopupCounters> _counters = new();

    private readonly ConcurrentDictionary<short, ConcurrentQueue<PopupRewardDue>> _rewardDue = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (!_rewardDue.TryGetValue(zone.MapId, out var queue))
            return;

        while (queue.TryDequeue(out var due))
            DeliverReward(zone, due);
    }

    public void NotifyPvpKill(Zone zone, PlayerRuntimeState killer, PlayerRuntimeState victim)
    {
        if (killer.CharacterId == victim.CharacterId)
            return;


        if (killer.CombinedLevel - victim.CombinedLevel > CombinedLevelGapCap)
            return;

        if (!PopupEventZoneCatalog.TryResolvePvpType(zone.MapId, out var type))
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
            if (counters.War >= threshold)
            {
                counters.War = 0;
                Enqueue(zone.MapId, type, killer.CharacterId);
                SendWarPopupCounter(killer, counters.War);
            }
        }
        else
        {
            counters.Avt = Math.Min(counters.Avt + 1, threshold);
            SendPopupKillCounters(killer, counters);
            if (counters.Avt >= threshold)
            {
                counters.Avt = 0;
                Enqueue(zone.MapId, type, killer.CharacterId);
                SendPopupKillCounters(killer, counters);
            }
        }
    }

    public void NotifyMonsterKill(Zone zone, PlayerRuntimeState killer, bool dropEligible)
    {
        if (!dropEligible)
            return;

        if (!PopupEventZoneCatalog.IsMonsterPopupMap(zone.MapId))
            return;

        if (!state.IsEnabled(PopupEventType.MonsterPve))
            return;

        var counters = _counters.GetValue(killer, static _ => new PopupCounters());
        counters.Monster = Math.Min(counters.Monster + 1, PopupEventZoneCatalog.MonsterKillThreshold);
        SendPopupKillCounters(killer, counters);
        if (counters.Monster >= PopupEventZoneCatalog.MonsterKillThreshold)
        {
            counters.Monster = 0;
            counters.Avt = 0;
            Enqueue(zone.MapId, PopupEventType.MonsterPve, killer.CharacterId);
            SendPopupKillCounters(killer, counters);
        }
    }

    // Un des deux seuls tSort qui portent tValue2 : les deux compteurs partent ensemble, meme depuis le
    // chemin monstre. Unicast au tueur, apres l'increment puis apres la remise a 0:
    // Server/ts25zone/S07_MyGame03.cpp:2629 ; :2643 ; Server/ts25zone/S07_MyGame05.cpp:2235 ; :2242
    private static void SendPopupKillCounters(PlayerRuntimeState killer, PopupCounters counters)
    {
        killer.Session.Send(new AvatarStatUpdateResponse
            { Sort = PopupKillCounterStatSort, Value = counters.Avt, Value2 = counters.Monster });
    }

    // Unicast au seul tueur, apres l'increment puis apres la remise a 0:
    // Server/ts25zone/S07_MyGame03.cpp:2603 ; Server/ts25zone/S07_MyGame03.cpp:2617
    private static void SendWarPopupCounter(PlayerRuntimeState killer, int counter)
    {
        killer.Session.Send(new AvatarStatUpdateResponse
            { Sort = WarPopupKillCounterStatSort, Value = counter, Value2 = 0 });
    }

    private void Enqueue(short mapId, PopupEventType type, int characterId)
    {
        var queue = _rewardDue.GetOrAdd(mapId, static _ => new ConcurrentQueue<PopupRewardDue>());
        queue.Enqueue(new PopupRewardDue(type, characterId));
    }

    private void DeliverReward(Zone zone, PopupRewardDue due)
    {
        if (!zone.TryGetPlayer(due.CharacterId, out _))
            return;

        zone.GrantWarPoints(due.CharacterId, WarPointRewardAmount);
    }

    private sealed class PopupCounters
    {
        public int Avt;

        public int Monster;

        public int War;
    }

    private readonly record struct PopupRewardDue(PopupEventType Type, int CharacterId);
}
