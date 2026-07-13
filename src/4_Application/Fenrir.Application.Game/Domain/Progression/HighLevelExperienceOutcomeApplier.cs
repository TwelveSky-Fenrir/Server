using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Progression;

public static class HighLevelExperienceOutcomeApplier
{
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
