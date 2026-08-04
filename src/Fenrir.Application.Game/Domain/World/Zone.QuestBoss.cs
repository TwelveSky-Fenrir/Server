using Fenrir.Application.Game.Domain.Quests;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
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
        TrySummonSpecialMonster(request.MonsterId, request.PosX, request.PosY, request.PosZ,
            true);
    }
}
