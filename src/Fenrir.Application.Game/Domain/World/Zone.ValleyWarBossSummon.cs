using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int ValleyWarBossPoolServerIndexBase = 1_005_000;

    private const int ValleyWarBossPoolSize = 10;

    internal void HandleSummonValleyWarBoss()
    {
        if (!worldData.MonstersById.TryGetValue(Zone200GateBreachBossCatalog.BossMonsterId, out var definition))
            return;

        if (Zone200GateBreachBossCatalog.ExistenceCheckActive && ValleyWarBossAlreadyLive())
            return;

        if (!TryFindFreeValleyWarBossSlot(out var serverIndex))
            return;

        var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster, serverIndex,
            Zone200GateBreachBossCatalog.SummonX, Zone200GateBreachBossCatalog.SummonY,
            Zone200GateBreachBossCatalog.SummonZ);

        SpawnMonster(monster);
    }

    internal bool ValleyWarBossSlotOccupied()
    {
        for (var i = 0; i < ValleyWarBossPoolSize; i++)
            if (_monsters.ContainsKey(ValleyWarBossPoolServerIndexBase + i))
                return true;

        return false;
    }

    private bool ValleyWarBossAlreadyLive()
    {
        for (var i = 0; i < ValleyWarBossPoolSize; i++)
        {
            var candidate = ValleyWarBossPoolServerIndexBase + i;
            if (_monsters.TryGetValue(candidate, out var monster) &&
                monster.Template.MonsterId == Zone200GateBreachBossCatalog.BossMonsterId)
                return true;
        }

        return false;
    }

    private bool TryFindFreeValleyWarBossSlot(out int serverIndex)
    {
        for (var i = 0; i < ValleyWarBossPoolSize; i++)
        {
            var candidate = ValleyWarBossPoolServerIndexBase + i;
            if (!_monsters.ContainsKey(candidate))
            {
                serverIndex = candidate;
                return true;
            }
        }

        serverIndex = 0;
        return false;
    }
}
