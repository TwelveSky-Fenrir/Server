namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Legacy <c>aKillMonsterNum2</c> -- <c>ProcessAttack03</c>'s personal 1000-kill CP milestone counter
    ///     (see <see cref="Progression.TowerCpForPvmMilestone" />). Deliberately in-memory-only, not
    ///     write-behind-tracked: no reviewed legacy citation covers this counter's own cross-session durability,
    ///     and every other Fenrir counter that IS persisted (<see cref="MissionKillOtherTribe" />,
    ///     <see cref="QuestKillCounter" />, ...) has an explicit Characters/CharacterProgressTvp column backing
    ///     it already -- adding one here is a product decision outside this contract's scope, not something to
    ///     invent. Resets to 0 on world re-entry, the same as every other PlayerRuntimeState field with no such
    ///     column.
    /// </summary>
    public int TowerCpMilestoneCounter { get; set; }
}
