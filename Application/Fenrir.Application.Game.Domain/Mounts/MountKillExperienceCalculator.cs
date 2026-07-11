namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     The mount-experience gain a killer earns on an inter-tribe kill while actively mounted
///     (<c>MyUtil::ProcessForKillOtherTribe</c>'s own mount branch). Pure amount resolution -- the sibling of
///     <see cref="Combat.PvpKillPetExperienceCalculator" />, computed independently and applied through the same
///     packed activity+experience encoder (<see cref="MountActivityExpCodec" />) the potion-feed and
///     persistence paths use. No I/O, no state.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:3190-3210 -- the gain fires only while the killer is actively
///     mounted, the mount's decoded activity is greater than 0, and its decoded experience is still below
///     <see cref="MountActivityExpCodec.MaxExp" /> (100,000). A configured base amount is doubled if the
///     character's mount-double-exp counter is running (legacy <c>aAnimalDoubleExp</c>,
///     <see cref="World.PlayerRuntimeState.AnimalDoubleExp" /> greater than 0) and doubled again if the session's
///     mount-XP-up flag is set (<see cref="World.PlayerRuntimeState.MountExpUp" />).
///     <para>
///         The activity-greater-than-0 gate is why an unfed mount never gains kill experience: activity is only
///         ever raised by feeding a mount-activity potion (<see cref="MountActivityExpCodec.FeedActivity" />), so
///         a mount left at activity 0 stays experience-frozen no matter how many kills its rider scores. This is
///         the whole of the source finding's "MountAccumulatedExp stuck at 0" observation that could be grounded
///         in the cited lines -- any claim of a distinct defect beyond this activity gate is carried forward
///         uncited and needs a <c>cpp-zone-gameplay-analyst</c> re-check.
///     </para>
/// </remarks>
public static class MountKillExperienceCalculator
{
    /// <summary>
    ///     The per-kill base mount-experience amount. Loaded in legacy from a deployment ini value, not a
    ///     compile-time constant -- no production number was plumbed to this contract, so this is a placeholder
    ///     (the same posture, and the same value, as
    ///     <see cref="Combat.PvpKillPetExperienceCalculator.PlaceholderPetExpRatio" />), NOT real game-balance
    ///     tuning. An operator-configurable <c>GameServerOptions</c> value should replace this constant once the
    ///     real deployment number is known -- see the workstream's wiring note.
    /// </summary>
    public const int PlaceholderBaseExperiencePerKill = 10;

    /// <summary>
    ///     The experience amount to add (before clamping) for this kill, or 0 when any gate fails. The caller
    ///     applies it as <c>MountActivityExpCodec.ClampExp(currentExp + gain)</c> and re-encodes.
    /// </summary>
    /// <param name="isMounted">Whether the killer is in the actively-mounted band (aAnimalIndex 10-19).</param>
    /// <param name="mountActivity">The active mount slot's decoded activity (0-100).</param>
    /// <param name="mountExperience">The active mount slot's decoded experience (0-100,000).</param>
    /// <param name="hasDoubleExp">Whether <c>aAnimalDoubleExp</c> is currently running (doubles the amount).</param>
    /// <param name="hasSessionExpUp">Whether the session's mount-XP-up flag is set (doubles the amount again).</param>
    /// <param name="baseAmount">The configured per-kill base (defaults to <see cref="PlaceholderBaseExperiencePerKill" />).</param>
    public static int ComputeGain(
        bool isMounted, int mountActivity, int mountExperience,
        bool hasDoubleExp, bool hasSessionExpUp,
        int baseAmount = PlaceholderBaseExperiencePerKill)
    {
        if (!isMounted || mountActivity <= 0 || mountExperience >= MountActivityExpCodec.MaxExp)
            return 0;

        var amount = baseAmount;
        if (hasDoubleExp)
            amount *= 2;
        if (hasSessionExpUp)
            amount *= 2;
        return amount;
    }
}
