namespace Fenrir.Application.Game.Domain.Quests;

public readonly record struct QuestProgress(
    int StepPermanent,
    int ActiveFlag,
    int QSort,
    int TargetPhase,
    int KillCounter)
{
    public static readonly QuestProgress None = default;

        public bool IsIdle => ActiveFlag == 0 && QSort == 0 && TargetPhase == 0 && KillCounter == 0;
}
