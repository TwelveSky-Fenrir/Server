namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Port of <c>MyUtil::ProcessForExperience</c>'s outer guard (S07_MyGame03.cpp:163-166): gates character
///     experience, pet experience, and the otherwise-independent health-value gain all together, as a single
///     all-or-nothing step.
/// </summary>
public static class MonsterKillExperienceGate
{
    /// <summary>
    ///     False (skip the entire step -- no character experience, no pet experience, no health-value gain)
    ///     when the avatar's session isn't ready, is mid zone-transfer, or both the character-experience and
    ///     pet-experience amounts are non-positive.
    /// </summary>
    public static bool ShouldProcess(bool isReady, bool isTransferringZone, int characterExperienceAmount,
        int petExperienceAmount)
    {
        if (!isReady || isTransferringZone)
            return false;

        return characterExperienceAmount >= 1 || petExperienceAmount >= 1;
    }
}
