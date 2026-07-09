namespace Fenrir.Application.Game.Domain.Gm;

/// <summary>
///     Posted by <c>Fenrir.Application.Game.Services.Gm.GmExpGrantService</c> (Elevated-tier GM-EXP command,
///     legacy PROCESS_DATA_SEND tSort 503) once the tier gate already passed, so the actual read-current-state
///     -&gt; decide -&gt; mutate work runs on the target zone's own tick thread instead of racing it -- same
///     "posted, already-decided-as-much-as-it-can-be-off-tick, applied on-tick" shape as
///     <see cref="Fenrir.Application.Game.Domain.Tribes.TribeProgressZoneCommand" />, except this command's own
///     mode-0 branch still needs to read the character's LIVE <c>Experience</c>/<c>Level2</c> at apply time (the
///     "already at cap" skip and "huge-magnitude shortcut" decisions cannot be made ahead of the tick that
///     applies them without risking a stale read), which is why this is its own dedicated command/channel
///     rather than one more nullable field folded into <see cref="Fenrir.Application.Game.Domain.Tribes.TribeProgressZoneCommand" />.
/// </summary>
/// <param name="CharacterId">A no-op if the character already left this zone by the time the tick drains this.</param>
/// <param name="Mode">
///     GmExpGrantPayload.Type verbatim: 0 = character (first-tier) experience, 1 = the invoking GM's own
///     equipped pet's experience (despite legacy's own inline comment mislabeling it "God tier / Rebirth" --
///     see <c>Zone.ApplyGmSelfExperienceGrantCommand</c>'s own remarks for why that label is wrong), any other
///     value (including the dead/commented-out mode 3) has no effect.
/// </param>
/// <param name="Magnitude">GmExpGrantPayload.Exp verbatim -- signed, no wire-level bound.</param>
/// <param name="Applied">Completed once actually mirrored -- see TribeProgressZoneCommand.Applied for why this matters while EconomyActionLock is held.</param>
public readonly record struct GmSelfExperienceGrantZoneCommand(
    int CharacterId,
    int Mode,
    int Magnitude,
    TaskCompletionSource? Applied = null);
