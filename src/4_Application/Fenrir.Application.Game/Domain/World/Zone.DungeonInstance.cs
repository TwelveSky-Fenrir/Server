using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

public enum DungeonInstanceEntryOutcome
{
    NotZone241Type,

    QuotaExhausted,

    SummonFailed,

    Entered
}

public sealed partial class Zone
{
    private const int PersonalDungeonBattleBroadcastCadenceTicks = 20;

    public IPersonalDungeonBossCatalog PersonalDungeonBossCatalog { get; set; } =
        NullPersonalDungeonBossCatalog.Instance;

    public bool IsZone241TypeZone =>
        options.Zone241DungeonMapIds.Contains(MapId) || WrapCheckSpecialDestinationCatalog.IsInstancedDestination(MapId);

    public DungeonInstanceEntryOutcome TryEnterZone241PersonalInstance(int characterId)
    {
        if (!IsZone241TypeZone)
            return DungeonInstanceEntryOutcome.NotZone241Type;

        if (!_players.TryGetValue(characterId, out var state) || state is null)
            return DungeonInstanceEntryOutcome.NotZone241Type;

        if (state.DungeonInstanceRoundsRemaining < 1)
            return DungeonInstanceEntryOutcome.QuotaExhausted;

        state.DungeonInstanceId = state.CharacterId;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Summoning;
        state.DungeonInstanceTick = 0;

        if (!PersonalDungeonBossCatalog.TryGetBossMonsterId(state.RebirthCount, out var monsterId) ||
            !worldData.MonstersById.TryGetValue(monsterId, out var monsterDefinition))
        {
            TearDownFailedEntry(state);
            return DungeonInstanceEntryOutcome.SummonFailed;
        }

        SummonPersonalBoss(state, monsterDefinition.Monster, PersonalDungeonBossTables.CatalogAAndESummonPosition);

        state.DungeonInstanceRoundsRemaining--;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.BattleInProgress;
        return DungeonInstanceEntryOutcome.Entered;
    }

    public DungeonInstanceEntryOutcome TryEnterLegendsOfDarknessInstance(int characterId)
    {
        if (!WrapCheckSpecialDestinationCatalog.IsInstancedDestination(MapId))
            return DungeonInstanceEntryOutcome.NotZone241Type;

        if (!_players.TryGetValue(characterId, out var state) || state is null)
            return DungeonInstanceEntryOutcome.NotZone241Type;

        state.DungeonInstanceId = characterId;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Summoning;
        state.DungeonInstanceTick = 0;

        if (!_monsters.TryGetValue(characterId, out var occupant) || occupant is null)
        {
            var bossMonsterId = PersonalDungeonBossTables.ResolveCatalogD(MapId);
            if (!worldData.MonstersById.TryGetValue(bossMonsterId, out var monsterDefinition))
            {
                TearDownFailedEntry(state);
                return DungeonInstanceEntryOutcome.SummonFailed;
            }

            SummonPersonalBoss(state, monsterDefinition.Monster, PersonalDungeonBossTables.CatalogDSummonPosition);
        }

        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.BattleInProgress;
        return DungeonInstanceEntryOutcome.Entered;
    }

    private void SummonPersonalBoss(PlayerRuntimeState state, MonsterRowDto template,
        (float X, float Y, float Z) spawnPosition)
    {
        var serverIndex = state.CharacterId;

        if (_monsters.TryRemove(serverIndex, out var displaced))
            RemoveMonsterFromGrid(displaced);

        var boss = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), template, serverIndex,
            spawnPosition.X, spawnPosition.Y, spawnPosition.Z, serverIndex);

        SpawnMonster(boss);
    }

    private void TearDownFailedEntry(PlayerRuntimeState state)
    {
        ClearZone241PersonalDungeonInstance(state);

        state.DungeonInstanceId = null;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Idle;
    }

    private void ClearDungeonInstanceOnDisconnect(PlayerRuntimeState state)
    {
        if (!IsZone241TypeZone)
            return;

        ClearZone241PersonalDungeonInstance(state);

        state.DungeonInstanceId = null;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Idle;
    }

    private void ClearZone241PersonalDungeonInstance(PlayerRuntimeState state)
    {
        if (state.DungeonInstanceLifecycleState != DungeonInstanceLifecycle.Summoning)
            return;

        if (state.DungeonInstanceId is not { } instanceId)
            return;

        foreach (var (index, monster) in _monsters)
            if (monster.InstanceId == instanceId && _monsters.TryRemove(index, out _))
                RemoveMonsterFromGrid(monster);

        foreach (var (index, item) in _groundItems)
            if (item.InstanceId == instanceId)
                _groundItems.TryRemove(index, out _);
    }

    private static bool IsVisibleAcrossDungeonInstance(int? objectInstanceId, int? viewerInstanceId)
    {
        return objectInstanceId is not { } required || required == viewerInstanceId;
    }

    public void AdvanceZone241PersonalDungeonInstances(int legacyTicksElapsed)
    {
        if (!IsZone241TypeZone)
            return;

        foreach (var state in _players.Values)
        {
            if (state.DungeonInstanceLifecycleState == DungeonInstanceLifecycle.Idle)
                continue;

            state.DungeonInstanceTick += legacyTicksElapsed;

            if (state.DungeonInstanceLifecycleState != DungeonInstanceLifecycle.BattleInProgress)
                continue;

            var instanceId = state.DungeonInstanceId ?? state.CharacterId;
            var bossAlive = _monsters.TryGetValue(instanceId, out var boss) && boss is not null &&
                            boss.InstanceId == instanceId;

            if (!bossAlive)
            {
                state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Success;
                ClearZone241PersonalDungeonInstance(state);
                continue;
            }

            if (state.DungeonInstanceTick % PersonalDungeonBattleBroadcastCadenceTicks == 0)
                state.Session.Send(new ZoneWar241StatusResponse { RemainTime = 0 });
        }
    }
}
