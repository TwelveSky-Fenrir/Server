using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int RegularWarBossPoolServerIndexBase = 1_004_000;

    private const int RegularWarBossPoolSize = 100;

    private void HandleSummonRegularWarBoss()
    {
        if (!worldData.MonstersById.TryGetValue(RegularWarBossSummonCatalog.BossMonsterId, out var definition))
            return;

        for (var i = 0; i < RegularWarBossSummonCatalog.SummonCount; i++)
        {
            if (!TryFindFreeRegularWarBossSlot(out var serverIndex))
                return;

            var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster,
                serverIndex, RegularWarBossSummonCatalog.SummonX, RegularWarBossSummonCatalog.SummonY,
                RegularWarBossSummonCatalog.SummonZ);

            SpawnMonster(monster);
        }
    }

    private void HandleDespawnRegularWarBosses()
    {
        for (var i = 0; i < RegularWarBossPoolSize; i++)
            DespawnMonsterSilently(RegularWarBossPoolServerIndexBase + i);
    }

    private bool TryFindFreeRegularWarBossSlot(out int serverIndex)
    {
        for (var i = 0; i < RegularWarBossPoolSize; i++)
        {
            var candidate = RegularWarBossPoolServerIndexBase + i;
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
