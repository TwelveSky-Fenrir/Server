namespace Fenrir.Application.Game.Quests;

/// <summary>
///     The legacy's entire quest state in 5 ints (wAvatar.aQuestInfo[5]), shared by
///     QuestStateMachine/PlayerRuntimeState/game.CharacterQuests.
/// </summary>
public readonly record struct QuestProgress(
    int StepPermanent,
    int ActiveFlag,
    int QSort,
    int TargetPhase,
    int KillCounter)
{
    public static readonly QuestProgress None = default;

    /// <summary>Legacy's own idle test -- deliberately not checking StepPermanent, which survives idle.</summary>
    public bool IsIdle => ActiveFlag == 0 && QSort == 0 && TargetPhase == 0 && KillCounter == 0;
}
