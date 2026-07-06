using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The reward/quest side effects <see cref="HolyStoneWarCycle" /> triggers on a successful capture
///     (contract steps 7-8) but does not implement itself -- the exact currency/rank-point amounts and the
///     quest marker id are owned by Combat/Commerce/Quest systems (and, per this cluster's own scope note,
///     are being touched by other concurrent workflows), and the translated behavior contract does not give
///     the exact reward amounts or quest marker id either. This interface is the seam:
///     <see cref="HolyStoneWarCycle" /> calls it at exactly the right point in the capture sequence, with
///     eligibility (tribe, distance, reborn-milestone, capturer-exclusion) already resolved by the caller --
///     the gateway itself only needs to grant/advance.
///     <para>
///         Contract step 9 (guard spawn) is NOT part of this seam -- it is already real, production behavior:
///         <see cref="HolyStoneWarCycle" /> calling <see cref="ZoneEventBroadcaster.AnnounceZone038Winner" />
///         for the possession change already forces the winning tribe's zone038-winner guard pool to resummon
///         on every zone (<see cref="TribeGuardSpawner.ForceZone038WinnerResummon" />, wired in
///         <see cref="ZoneEventBroadcaster.AnnounceZone038Winner" /> itself), so no separate call is needed
///         here.
///     </para>
/// </summary>
public interface IHolyStoneCaptureRewardGateway
{
    /// <summary>The capturing character's own reward -- contract: "two separate currency/rank-point grants... that both apply and add together."</summary>
    void GrantCaptureReward(PlayerRuntimeState capturer);

    /// <summary>One tribemate's participation reward -- caller has already checked radius, reborn milestone, and capturer-exclusion.</summary>
    void GrantParticipationReward(PlayerRuntimeState tribemate);

    /// <summary>One tribemate's quest-progress advance -- caller has already checked tribe/online/non-dead; the quest-marker/turn-in-state gate itself lives here.</summary>
    void AdvanceQuestProgress(PlayerRuntimeState tribemate);
}

/// <summary>
///     Logs every call instead of granting/advancing anything -- the safe placeholder until real reward
///     amounts and the quest marker id are known. Wiring this in production means
///     <see cref="HolyStoneWarCycle" />'s own sequencing/eligibility logic runs correctly (the right
///     characters are identified, in the right order, with the right gates) but nothing is actually granted
///     yet.
/// </summary>
public sealed class LoggingOnlyHolyStoneCaptureRewardGateway(ILogger<LoggingOnlyHolyStoneCaptureRewardGateway> logger)
    : IHolyStoneCaptureRewardGateway
{
    public void GrantCaptureReward(PlayerRuntimeState capturer)
    {
        logger.LogWarning(
            "HolyStoneWar: character {CharacterId} captured the Stone but no reward gateway is wired yet -- no currency/rank-point grant applied",
            capturer.CharacterId);
    }

    public void GrantParticipationReward(PlayerRuntimeState tribemate)
    {
        logger.LogWarning(
            "HolyStoneWar: character {CharacterId} qualifies for the tribe-wide participation grant but no reward gateway is wired yet",
            tribemate.CharacterId);
    }

    public void AdvanceQuestProgress(PlayerRuntimeState tribemate)
    {
        logger.LogWarning(
            "HolyStoneWar: character {CharacterId} is a candidate for the capture quest-progress advance but no quest gateway is wired yet",
            tribemate.CharacterId);
    }
}
