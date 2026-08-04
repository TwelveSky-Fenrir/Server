using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Protocol.Game;

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

    private const int PersonalDungeonBattleBudgetLegacyTicks = 3600;

    private const int PersonalDungeonReturnToTownDelayLegacyTicks = 120;

    private const int PersonalDungeonDisconnectDelayLegacyTicks = 10;

    public IPersonalDungeonBossCatalog PersonalDungeonBossCatalog { get; set; } =
        NullPersonalDungeonBossCatalog.Instance;

    public bool ChallengeContentEnabled => options.ChallengeContentEnabled;

    public bool IsTimedChallengeMap => ChallengeContentEnabled && MapId is >= 234 and <= 240;

    public bool IsZone241TypeZone => ChallengeContentEnabled &&
                                      (options.Zone241DungeonMapIds.Contains(MapId) ||
                                       WrapCheckSpecialDestinationCatalog.IsInstancedDestination(MapId));

    public DungeonInstanceEntryOutcome TryEnterZone241PersonalInstance(int characterId)
    {
        if (!IsZone241TypeZone)
            return DungeonInstanceEntryOutcome.NotZone241Type;

        if (!_players.TryGetValue(characterId, out var state) || state is null)
            return DungeonInstanceEntryOutcome.NotZone241Type;

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

        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.BattleInProgress;
        return DungeonInstanceEntryOutcome.Entered;
    }

    public DungeonInstanceEntryOutcome TryEnterLegendsOfDarknessInstance(int characterId)
    {
        if (!ChallengeContentEnabled || !WrapCheckSpecialDestinationCatalog.IsInstancedDestination(MapId))
            return DungeonInstanceEntryOutcome.NotZone241Type;

        if (!_players.TryGetValue(characterId, out var state) || state is null)
            return DungeonInstanceEntryOutcome.NotZone241Type;

        state.DungeonInstanceId = characterId;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Summoning;
        state.DungeonInstanceTick = 0;

        if (!_monsters.TryGetValue(characterId, out var occupant) || occupant is null || occupant.Life <= 0)
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
        {
            _monsterPursuers.Untrack(displaced);
            RemoveMonsterFromGrid(displaced);
        }

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
        if (state.DungeonInstanceLifecycleState == DungeonInstanceLifecycle.Idle)
            return;

        if (state.DungeonInstanceId is not { } instanceId)
            return;

        var monstersRemoved = false;
        foreach (var (index, monster) in _monsters)
        {
            if (monster.InstanceId != instanceId || !_monsters.TryRemove(index, out var removedMonster))
                continue;

            _monsterPursuers.Untrack(removedMonster);
            RemoveMonsterFromGrid(removedMonster);
            monstersRemoved = true;
        }

        if (monstersRemoved)
            RefreshMonsterOrder();

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

            switch (state.DungeonInstanceLifecycleState)
            {
                case DungeonInstanceLifecycle.BattleInProgress:
                    AdvancePersonalDungeonBattle(state);
                    break;

                case DungeonInstanceLifecycle.Success:
                case DungeonInstanceLifecycle.Failure:
                    state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.WaitBeforeReturn;
                    state.DungeonInstanceTick = 0;
                    break;

                case DungeonInstanceLifecycle.WaitBeforeReturn:
                    if (state.DungeonInstanceTick >= PersonalDungeonReturnToTownDelayLegacyTicks)
                    {
                        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.ReturnToTownNotice;
                        state.DungeonInstanceTick = 0;
                    }

                    break;

                case DungeonInstanceLifecycle.ReturnToTownNotice:
                    state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.DisconnectPending;
                    state.DungeonInstanceTick = 0;
                    break;

                case DungeonInstanceLifecycle.DisconnectPending:
                    if (state.DungeonInstanceTick >= PersonalDungeonDisconnectDelayLegacyTicks)
                        EvictAtPersonalDungeonEnd(state);

                    break;
            }
        }
    }

    private void AdvancePersonalDungeonBattle(PlayerRuntimeState state)
    {
        var tick = state.DungeonInstanceTick;

        if (tick < PersonalDungeonBattleBroadcastCadenceTicks)
            return;

        if (tick % PersonalDungeonBattleBroadcastCadenceTicks == 0)
            state.Session.Send(new ZoneWar241StatusResponse
            {
                RemainTime = PersonalDungeonBattleBudgetLegacyTicks - tick
            });

        if (tick >= PersonalDungeonBattleBudgetLegacyTicks || state.IsMovingZone)
        {
            state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Failure;
            return;
        }

        var instanceId = state.DungeonInstanceId ?? state.CharacterId;
        var bossAlive = _monsters.TryGetValue(instanceId, out var boss) && boss is not null &&
                        boss.InstanceId == instanceId && boss.Life > 0;

        if (!bossAlive)
            state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Success;
    }

    private void EvictAtPersonalDungeonEnd(PlayerRuntimeState state)
    {
        ClearZone241PersonalDungeonInstance(state);

        state.DungeonInstanceId = null;
        state.DungeonInstanceLifecycleState = DungeonInstanceLifecycle.Idle;

        state.Session.Abort(DisconnectReason.TimedZoneExpired);
    }
}
