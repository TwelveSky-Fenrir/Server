using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World.Monsters;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int QuestBossPoolServerIndexBase = 1_006_000;

    private const int QuestBossPoolSize = 100;

    internal void SummonPersonalQuestBossesForTick()
    {
        foreach (var (_, state) in _players)
            TrySummonPersonalQuestBoss(state);
    }

    private void TrySummonPersonalQuestBoss(PlayerRuntimeState state)
    {
        if (state.QuestSort != QuestBossResummon.TriggerQuestSort)
            return;

        var quest = _questCatalog.TryGet(state.Tribe, state.QuestStepPermanent);
        if (quest is null)
            return;

        var progress = new QuestProgress(state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort,
            state.QuestTargetPhase, state.QuestKillCounter);

        var presentState = QuestStateMachine.ComputePresentState(progress, state.Tribe, state.Level, _questCatalog,
            static _ => false);

        var request = QuestBossResummon.Evaluate(state.QuestSort, presentState, quest, MapId, state.PosX,
            state.PosY, state.PosZ);
        if (request is not { } summon)
            return;

        SummonPersonalQuestBoss(summon);
    }

    private void SummonPersonalQuestBoss(QuestBossSummonRequest request)
    {
        if (QuestBossAlreadyLive(request.MonsterId))
            return;

        if (!TryFindFreeQuestBossSlot(out var serverIndex))
            return;

        if (!worldData.MonstersById.TryGetValue(request.MonsterId, out var definition))
            return;

        var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster, serverIndex,
            request.PosX, request.PosY, request.PosZ);

        SpawnMonster(monster);
    }

    private bool QuestBossAlreadyLive(int monsterId)
    {
        for (var i = 0; i < QuestBossPoolSize; i++)
        {
            var candidate = QuestBossPoolServerIndexBase + i;
            if (_monsters.TryGetValue(candidate, out var monster) && monster.Template.MonsterId == monsterId)
                return true;
        }

        return false;
    }

    private bool TryFindFreeQuestBossSlot(out int serverIndex)
    {
        for (var i = 0; i < QuestBossPoolSize; i++)
        {
            var candidate = QuestBossPoolServerIndexBase + i;
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
