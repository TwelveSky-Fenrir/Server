namespace Fenrir.Application.Game.Quests;

/// <summary>
///     The legacy's entire quest state in 5 ints (<c>wAvatar.aQuestInfo[5]</c>, report 04 §5) -- a snapshot
///     type shared by <see cref="QuestStateMachine" /> (pure logic) and
///     <see cref="Fenrir.Application.Game.World.PlayerRuntimeState" /> (live state)/game.CharacterQuests
///     (persistence), so all three never drift out of field-order sync.
/// </summary>
public readonly record struct QuestProgress(
    int StepPermanent,
    int ActiveFlag,
    int QSort,
    int TargetPhase,
    int KillCounter)
{
    public static readonly QuestProgress None = default;

    /// <summary>
    ///     Legacy's own idle test: <c>[1]==0 &amp;&amp; [2]==0 &amp;&amp; [3]==0 &amp;&amp; [4]==0</c> -- deliberately
    ///     NOT checking <see cref="StepPermanent" />, which survives idle.
    /// </summary>
    public bool IsIdle => ActiveFlag == 0 && QSort == 0 && TargetPhase == 0 && KillCounter == 0;
}
