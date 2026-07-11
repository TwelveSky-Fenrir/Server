using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Applies one resolved <see cref="HighLevelExperienceOutcome" /> to the awarding character's own runtime
///     counters -- the second half of the B17-rebirth-hook adapter pair (<see cref="HighLevelExperienceInputFactory" />
///     builds the resolver's input; this applies its result). Deliberately narrow: only mutates the plain
///     counters the outcome itself carries (<see cref="PlayerRuntimeState.Experience" />/
///     <see cref="PlayerRuntimeState.Level2" />/<see cref="PlayerRuntimeState.Exp2" />/
///     <see cref="PlayerRuntimeState.StatPoints" />/<see cref="PlayerRuntimeState.SkillPoints" />/
///     <see cref="PlayerRuntimeState.Zone101Time" />) -- no I/O, no derived-stat recompute, no broadcast, no
///     dirty-tracker mark. A <see cref="HighLevelExperienceOutcomeKind.RebirthTierLevelUp" /> outcome still
///     needs a caller with zone context (equipment container, <c>WorldDataCache</c>, the AOI broadcast
///     helpers) to recompute derived combat stats, heal, mark the character dirty, and notify the client/AOI
///     -- see <c>Zone.HighLevelExperience.cs</c>'s <c>ApplyHighLevelExperienceGain</c> for that orchestration,
///     which calls this method first and then performs exactly those zone-scoped steps.
/// </summary>
public static class HighLevelExperienceOutcomeApplier
{
    /// <summary>
    ///     Applies <paramref name="outcome" /> to <paramref name="target" />. A no-op for
    ///     <see cref="HighLevelExperienceOutcomeKind.None" />.
    /// </summary>
    public static void Apply(PlayerRuntimeState target, in HighLevelExperienceOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case HighLevelExperienceOutcomeKind.None:
                break;

            case HighLevelExperienceOutcomeKind.MainPoolFill:
                target.Experience = outcome.NewMainExperience;
                target.StatPoints += outcome.StatPointsGranted;
                break;

            case HighLevelExperienceOutcomeKind.RebirthTierLevelUp:
                target.Level2 = outcome.NewLevel2;
                target.Exp2 = outcome.NewExp2;
                target.SkillPoints += outcome.SkillPointsGranted;
                if (outcome.Zone101TimeBonus > 0)
                    target.Zone101Time += outcome.Zone101TimeBonus;
                break;

            case HighLevelExperienceOutcomeKind.RebirthTierAccrual:
                target.Exp2 = outcome.NewExp2;
                target.StatPoints += outcome.StatPointsGranted;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind,
                    "Unhandled HighLevelExperienceOutcomeKind.");
        }
    }
}
