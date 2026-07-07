using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>Where one (post one, post two) leader pairing sits in the alliance ceremony's own state machine.</summary>
public enum AllianceCeremonyPhase
{
    Idle,
    RejectionMessage,
    NewAllianceNegotiation,
    AlreadyAlliedNegotiation,
    PostNegotiationCooldown
}

/// <summary>The 5 player-facing status codes this ceremony can notify the two identified tribe leaders with.</summary>
public enum AllianceCeremonyNotice
{
    /// <summary>No notice this tick.</summary>
    None,

    /// <summary>Rejected/cannot-proceed -- one of the not-currently-allied disqualifiers fired.</summary>
    Rejected,

    /// <summary>New-alliance path, in progress -- carries the remaining countdown.</summary>
    NewAllianceProgress,

    /// <summary>New-alliance path, aborted by a leader losing eligibility mid-negotiation.</summary>
    NewAllianceAborted,

    /// <summary>Already-allied (dissolution) path, in progress -- carries the remaining countdown.</summary>
    AlreadyAlliedProgress,

    /// <summary>Already-allied (dissolution) path, aborted by a leader losing eligibility mid-negotiation.</summary>
    AlreadyAlliedAborted
}

/// <summary>An already-detected, already-qualifying tribe leader occupying one of the two ceremony posts.</summary>
public readonly record struct AllianceCeremonyCandidate(int CharacterId, byte TribeId);

/// <summary>
///     One tick's outcome: <see cref="Notice" /> is <see cref="AllianceCeremonyNotice.None" /> on most ticks
///     (nothing changed); when it is not, <see cref="RecipientOne" />/<see cref="RecipientTwo" /> are the two
///     leaders to notify (both always present together -- this ceremony never notifies just one side).
/// </summary>
public readonly record struct AllianceCeremonyTickResult(
    AllianceCeremonyNotice Notice,
    AllianceCeremonyCandidate? RecipientOne,
    AllianceCeremonyCandidate? RecipientTwo,
    int RemainingCountdown)
{
    public static readonly AllianceCeremonyTickResult None = new(AllianceCeremonyNotice.None, null, null, 0);
}

/// <summary>
///     <c>Process_Allience_Server</c>'s 5-state ceremony: two tribes' elected leaders standing simultaneously
///     at two fixed map posts either dissolve their existing alliance, or (per a dead-end bug -- see remarks)
///     go through the motions of forming a new one without it ever actually taking effect.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3764-4012 (<c>Process_Allience_Server</c>) -- idle
///     scan/detection (3781-3915), rejection-message state (3916-3923), new-alliance-negotiation state
///     (3924-3961), already-allied-negotiation state (3962-4002), post-negotiation-cooldown state
///     (4003-4011) ; :3773-3778 (the per-tick raw counter) ; :586-611 (server-number-37 +
///     <c>AllianceTribe</c> arm gate and the two posts' hardcoded coordinates/radii) ; Server/Header/function.h:2964-2980
///     (<c>ReturnAlliance</c>/<c>ReturnAllianceTribe</c>), :2994-3007 (<c>ReturnTribePointWithAlliance</c>) ;
///     Server/Header/function.h:1642-1652 (<c>GetGameTickMinute</c>/<c>GetGameTickHour</c> -- the 120/7200 raw
///     tick conventions the rejection-message and post-negotiation-cooldown states use; the two negotiation
///     states instead use a bare "every other raw tick" check unrelated to this convention) ;
///     Server/ts25center/S04_MyWork02.cpp:427-448 (case 47, already-allied path's dissolve + 14-day cooldown)
///     and :408-425 (case 46, "form a new pairing" -- confirmed zero send sites anywhere in <c>Server/</c>).
///     <para>
///         EDGE CASE (preserved, not fixed): completing new-alliance-negotiation's countdown never notifies
///         the central authority at all (S07_MyGame01.cpp:3924-3961's success path has no case-46 send) -- it
///         just advances to <see cref="AllianceCeremonyPhase.PostNegotiationCooldown" />. No currently-existing
///         legacy code path can ever cause two previously-unallied tribes to become allied; only the reverse
///         (dissolving an existing alliance via the already-allied path) is reachable. This class reproduces
///         that dead end exactly: its own new-alliance-negotiation completion calls no
///         <see cref="WorldStateService" />/<see cref="ZoneEventBroadcaster" /> mutation.
///     </para>
///     <para>
///         GAP 1 (idle-state detection) -- RESOLVED: the two posts' current qualifying leader, if any, is now
///         produced by <see cref="AlliancePostOccupantScanner.Scan" /> (S07_MyGame01.cpp:3781-3869's per-post
///         idle-scan loops; both fixed posts' literal world-space location/radius are cited at
///         S07_MyGame01.cpp:593-600 combined with Server/ts25zone/H07_MyGame.h:138-139) and fed into
///         <see cref="Tick" />'s two candidate parameters every legacy tick by
///         <c>Fenrir.Application.Game.Hosting.World.ZoneWar.AllianceDiplomacyCeremonyHost</c> -- see that
///         scanner's own remarks for the exact per-qualifier translation, including the one still-open
///         sub-gap ("hiding," which has no <see cref="PlayerRuntimeState" /> equivalent anywhere in Fenrir
///         yet and is therefore skipped, not invented). <see cref="Tick" /> itself deliberately still takes
///         plain candidate inputs rather than performing the scan inline, so this class's own state-machine
///         tests keep driving it directly with hand-built candidates instead of a live zone/registry.
///     </para>
///     <para>
///         GAP 2 (negotiation countdown length) -- RESOLVED: both negotiation phases' fresh-countdown
///         starting value is <see cref="NegotiationConfirmationDurationRawTicks" /> (S07_MyGame01.cpp:3904 --
///         the new-alliance path's <c>mAllienceRemainTime = 60</c> -- and :3912, the already-allied path's
///         identical assignment; both decrement once per "every OTHER raw tick" check, :3925/:3963, matching
///         this class's own <see cref="AdvanceNegotiation" /> parity gate exactly). Production wiring
///         (<c>HostingServiceCollectionExtensions.AddZoneWar</c>) now passes this constant for both
///         constructor arguments; the constructor itself still requires them explicitly rather than
///         hardcoding the constant internally, so this class's own unit tests can keep exercising the
///         countdown mechanics with small values instead of waiting out 60 real negotiation ticks per test --
///         see
///         <see
///             cref="AllianceDiplomacyCeremony(WorldStateService,AllianceCooldownTracker,ZoneEventBroadcaster,ILogger{AllianceDiplomacyCeremony},int,int)" />
///         .
///     </para>
///     <para>
///         GAP 3 (player-facing wire messages, not implemented here): <see cref="AllianceCeremonyTickResult" />
///         exposes the outcome as a plain result the caller translates into whatever packet
///         <c>fenrir-wire-protocol-implementer</c> eventually defines for these 5 status codes -- no such
///         packet exists in <c>Network.Serialization.Packets.Zone</c> yet, so this class deliberately does not
///         attempt to send anything itself. <c>AllianceDiplomacyCeremonyHost</c> logs the outcome instead, in
///         the meantime -- still open, not resolved by this change.
///     </para>
/// </remarks>
public sealed class AllianceDiplomacyCeremony
{
    /// <summary>ReturnTribePointWithAlliance floor for either side of a not-yet-allied pairing (function.h:2994-3007).</summary>
    public const int MinimumAllyAdjustedPoints = 100;

    /// <summary>GetGameTickMinute(1) -- S07_MyGame01.cpp:3916-3923.</summary>
    public const int RejectionMessageDurationRawTicks = 120;

    /// <summary>GetGameTickHour(1) -- S07_MyGame01.cpp:4003-4011.</summary>
    public const int PostNegotiationCooldownDurationRawTicks = 7200;

    /// <summary>
    ///     <c>mAllienceRemainTime = 60</c>, identical for both negotiation phases (S07_MyGame01.cpp:3904
    ///     new-alliance path, :3912 already-allied path). Resolves this class's own former GAP 2 -- see the
    ///     class remarks. Not applied automatically by the constructor (see its own remarks for why); use
    ///     this constant explicitly when constructing for production use.
    /// </summary>
    public const int NegotiationConfirmationDurationRawTicks = 60;

    /// <summary>S04_MyWork02.cpp:427-448 case 47's mutual re-alliance cooldown length.</summary>
    public const int ReAllianceCooldownDays = 14;

    private readonly int _alreadyAlliedNegotiationDurationRawTicks;
    private readonly ZoneEventBroadcaster _broadcaster;

    private readonly AllianceCooldownTracker _cooldowns;
    private readonly Lock _lock = new();
    private readonly ILogger<AllianceDiplomacyCeremony> _logger;
    private readonly int _newAllianceNegotiationDurationRawTicks;
    private readonly WorldStateService _worldState;

    private AllianceCeremonyCandidate? _leaderOne;
    private AllianceCeremonyCandidate? _leaderTwo;
    private AllianceCeremonyPhase _phase = AllianceCeremonyPhase.Idle;
    private int _phaseEnteredAtRawTick;
    private int _rawTick;
    private int _remainingCountdown;

    /// <param name="newAllianceNegotiationDurationRawTicks">
    ///     See GAP 2 in this class's own remarks -- no legacy-cited default
    ///     exists; must be positive.
    /// </param>
    /// <param name="alreadyAlliedNegotiationDurationRawTicks">
    ///     Same caveat as
    ///     <paramref name="newAllianceNegotiationDurationRawTicks" />.
    /// </param>
    public AllianceDiplomacyCeremony(
        WorldStateService worldState,
        AllianceCooldownTracker cooldowns,
        ZoneEventBroadcaster broadcaster,
        ILogger<AllianceDiplomacyCeremony> logger,
        int newAllianceNegotiationDurationRawTicks,
        int alreadyAlliedNegotiationDurationRawTicks)
    {
        if (newAllianceNegotiationDurationRawTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(newAllianceNegotiationDurationRawTicks),
                newAllianceNegotiationDurationRawTicks, "Must be positive.");
        if (alreadyAlliedNegotiationDurationRawTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(alreadyAlliedNegotiationDurationRawTicks),
                alreadyAlliedNegotiationDurationRawTicks, "Must be positive.");

        _worldState = worldState;
        _cooldowns = cooldowns;
        _broadcaster = broadcaster;
        _logger = logger;
        _newAllianceNegotiationDurationRawTicks = newAllianceNegotiationDurationRawTicks;
        _alreadyAlliedNegotiationDurationRawTicks = alreadyAlliedNegotiationDurationRawTicks;
    }

    public AllianceCeremonyPhase Phase
    {
        get
        {
            lock (_lock)
            {
                return _phase;
            }
        }
    }

    /// <summary>
    ///     Advances the ceremony by exactly one raw tick. <paramref name="postOneLeader" />/
    ///     <paramref name="postTwoLeader" /> are the current tick's already-filtered qualifying-leader
    ///     detections at each post (null if neither post currently qualifies) -- see GAP 1 in this class's own
    ///     remarks for why this class does not perform that detection itself. <paramref name="today" /> drives
    ///     the cooldown disqualifier and the post-dissolution cooldown date, evaluated fresh every call.
    /// </summary>
    public AllianceCeremonyTickResult Tick(AllianceCeremonyCandidate? postOneLeader,
        AllianceCeremonyCandidate? postTwoLeader, DateOnly today)
    {
        lock (_lock)
        {
            _rawTick++;

            return _phase switch
            {
                AllianceCeremonyPhase.Idle => EvaluateIdle(postOneLeader, postTwoLeader, today),
                AllianceCeremonyPhase.RejectionMessage => AdvanceRejection(),
                AllianceCeremonyPhase.NewAllianceNegotiation => AdvanceNegotiation(postOneLeader, postTwoLeader,
                    false, today),
                AllianceCeremonyPhase.AlreadyAlliedNegotiation => AdvanceNegotiation(postOneLeader, postTwoLeader,
                    true, today),
                AllianceCeremonyPhase.PostNegotiationCooldown => AdvanceCooldown(),
                _ => AllianceCeremonyTickResult.None
            };
        }
    }

    private AllianceCeremonyTickResult EvaluateIdle(AllianceCeremonyCandidate? postOneLeader,
        AllianceCeremonyCandidate? postTwoLeader, DateOnly today)
    {
        if (postOneLeader is not { } one || postTwoLeader is not { } two)
            return AllianceCeremonyTickResult.None;

        var isAlreadyAllied = _worldState.GetAllyOf(one.TribeId) == two.TribeId;

        if (isAlreadyAllied)
        {
            EnterNegotiation(AllianceCeremonyPhase.AlreadyAlliedNegotiation, one, two,
                _alreadyAlliedNegotiationDurationRawTicks);
            return AllianceCeremonyTickResult.None;
        }

        if (IsDisqualified(one.TribeId, two.TribeId, today))
        {
            EnterRejection();
            _logger.LogInformation(
                "Alliance ceremony rejected pairing of tribe {TribeA} and tribe {TribeB}", one.TribeId, two.TribeId);
            return new AllianceCeremonyTickResult(AllianceCeremonyNotice.Rejected, one, two, 0);
        }

        EnterNegotiation(AllianceCeremonyPhase.NewAllianceNegotiation, one, two,
            _newAllianceNegotiationDurationRawTicks);
        return AllianceCeremonyTickResult.None;
    }

    private AllianceCeremonyTickResult AdvanceRejection()
    {
        if (_rawTick - _phaseEnteredAtRawTick >= RejectionMessageDurationRawTicks)
            TransitionToIdle();

        return AllianceCeremonyTickResult.None;
    }

    private AllianceCeremonyTickResult AdvanceCooldown()
    {
        if (_rawTick - _phaseEnteredAtRawTick >= PostNegotiationCooldownDurationRawTicks)
            TransitionToIdle();

        return AllianceCeremonyTickResult.None;
    }

    private AllianceCeremonyTickResult AdvanceNegotiation(AllianceCeremonyCandidate? postOneLeader,
        AllianceCeremonyCandidate? postTwoLeader, bool isAlreadyAllied, DateOnly today)
    {
        // The legacy re-validates and decrements every OTHER raw tick -- a bare parity check, unrelated to the
        // GetGameTickMinute/Hour convention the rejection/cooldown states use (see class remarks).
        if (_rawTick % 2 != 0)
            return AllianceCeremonyTickResult.None;

        var leaderOne = _leaderOne!.Value;
        var leaderTwo = _leaderTwo!.Value;

        var stillValid = postOneLeader is { } one && postTwoLeader is { } two &&
                         one == leaderOne && two == leaderTwo;

        if (!stillValid)
        {
            TransitionToIdle();
            return new AllianceCeremonyTickResult(
                isAlreadyAllied
                    ? AllianceCeremonyNotice.AlreadyAlliedAborted
                    : AllianceCeremonyNotice.NewAllianceAborted,
                leaderOne, leaderTwo, 0);
        }

        _remainingCountdown--;

        if (_remainingCountdown > 0)
            return new AllianceCeremonyTickResult(
                isAlreadyAllied
                    ? AllianceCeremonyNotice.AlreadyAlliedProgress
                    : AllianceCeremonyNotice.NewAllianceProgress,
                leaderOne, leaderTwo, _remainingCountdown);

        if (isAlreadyAllied)
        {
            var eligibleAgain = today.AddDays(ReAllianceCooldownDays);
            _cooldowns.SetCooldownUntil(leaderOne.TribeId, eligibleAgain);
            _cooldowns.SetCooldownUntil(leaderTwo.TribeId, eligibleAgain);
            _broadcaster.AnnounceAllianceDissolved(leaderOne.TribeId, leaderTwo.TribeId);

            _logger.LogInformation(
                "Alliance ceremony dissolved the alliance between tribe {TribeA} and tribe {TribeB}, both under cooldown until {EligibleAgain:d}",
                leaderOne.TribeId, leaderTwo.TribeId, eligibleAgain);
        }

        // New-alliance path: dead end as coded -- no WorldStateService/ZoneEventBroadcaster call, see class remarks.

        EnterPostNegotiationCooldown();
        return AllianceCeremonyTickResult.None;
    }

    private void EnterNegotiation(AllianceCeremonyPhase phase, AllianceCeremonyCandidate one,
        AllianceCeremonyCandidate two, int durationRawTicks)
    {
        _phase = phase;
        _phaseEnteredAtRawTick = _rawTick;
        _leaderOne = one;
        _leaderTwo = two;
        _remainingCountdown = durationRawTicks;
    }

    private void EnterRejection()
    {
        _phase = AllianceCeremonyPhase.RejectionMessage;
        _phaseEnteredAtRawTick = _rawTick;
        _leaderOne = null;
        _leaderTwo = null;
    }

    private void EnterPostNegotiationCooldown()
    {
        _phase = AllianceCeremonyPhase.PostNegotiationCooldown;
        _phaseEnteredAtRawTick = _rawTick;
        _leaderOne = null;
        _leaderTwo = null;
    }

    private void TransitionToIdle()
    {
        _phase = AllianceCeremonyPhase.Idle;
        _leaderOne = null;
        _leaderTwo = null;
    }

    /// <summary>
    ///     Every disqualifier is re-evaluated fresh, never cached -- a pairing rejected on one attempt can
    ///     become eligible later if tribe standings/cooldowns change, and vice versa (S07_MyGame01.cpp:3781-3915).
    /// </summary>
    private bool IsDisqualified(byte tribeA, byte tribeB, DateOnly today)
    {
        var highestTribe = GetHighestAllyAdjustedPointsTribe();
        if (highestTribe == tribeA || highestTribe == tribeB)
            return true;

        if (_worldState.GetAllyOf(tribeA) is not null || _worldState.GetAllyOf(tribeB) is not null)
            return true;

        if (GetAllyAdjustedPoints(tribeA) < MinimumAllyAdjustedPoints ||
            GetAllyAdjustedPoints(tribeB) < MinimumAllyAdjustedPoints)
            return true;

        return _cooldowns.IsInCooldown(tribeA, today) || _cooldowns.IsInCooldown(tribeB, today);
    }

    /// <summary>ReturnTribePointWithAlliance (function.h:2994-3007): own points, plus the current ally's if any.</summary>
    private int GetAllyAdjustedPoints(byte tribeId)
    {
        var points = _worldState.GetTribe(tribeId).Points;
        if (_worldState.GetAllyOf(tribeId) is { } allyTribeId)
            points += _worldState.GetTribe(allyTribeId).Points;

        return points;
    }

    /// <summary>Null if two or more tribes are tied for the highest ally-adjusted total -- no single "biggest tribe" then.</summary>
    private byte? GetHighestAllyAdjustedPointsTribe()
    {
        Span<int> points = stackalloc int[WorldStateService.TribeCount];
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            points[tribeId] = GetAllyAdjustedPoints(tribeId);

        var max = int.MinValue;
        foreach (var value in points)
            if (value > max)
                max = value;

        byte? highestTribe = null;
        var tiedForMax = 0;
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            if (points[tribeId] == max)
            {
                tiedForMax++;
                highestTribe = tribeId;
            }

        return tiedForMax == 1 ? highestTribe : null;
    }
}
