using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>The per-cycle Force Leader election window, mirroring legacy <c>mWorldInfo-&gt;mTribeVoteState</c>.</summary>
public enum TribeVotePhase : byte
{
    /// <summary>Default/reset/idle state (legacy 0) -- CZ_TRIBE_VOTE_SEND sort 1 and 3 both reject.</summary>
    Closed = 0,

    /// <summary>Legacy 1, "registration-open" -- sort 1 (candidacy) accepted, sort 3 (vote) rejected.</summary>
    Candidacy = 1,

    /// <summary>Legacy 2, "voting-open" -- sort 3 (vote) accepted, sort 1 (candidacy) rejected.</summary>
    Voting = 2,

    /// <summary>
    ///     Legacy 3 (tSort 54, TRIBE_WORK "close voting"): voting has closed but results are not tallied yet
    ///     -- sort 1 and 3 both reject, same as <see cref="Closed" />, but distinguished here so
    ///     <see cref="TribeVoteElectionCalendar" />'s own "already at this bucket" idempotency checks can tell
    ///     it apart from a fresh/idle cycle.
    /// </summary>
    VotingClosed = 3,

    /// <summary>
    ///     Legacy 4 (tSort 55, TRIBE_WORK "announce results"): the Force Leader tally has run -- sort 1 and 3
    ///     both reject, same as <see cref="Closed" />, distinguished for the same reason as
    ///     <see cref="VotingClosed" />.
    /// </summary>
    ResultsAnnounced = 4
}

/// <summary>Result of <see cref="TribeVoteElection.TryRegisterCandidacyAsync" />.</summary>
public enum TribeVoteCandidacyOutcome
{
    Registered,
    WindowClosed,
    LevelTooLow,
    NotEnoughContribution,
    AlreadyRegisteredInAnotherSlot,
    SlotHeldByStrongerCandidate
}

/// <summary>Result of <see cref="TribeVoteElection.TryCastVoteAsync" />.</summary>
public enum TribeVoteCastOutcome
{
    Cast,
    WindowClosed,
    LevelTooLow,
    SlotEmpty,

    /// <summary>
    ///     The only rejection that is NOT a disconnect in the legacy (S04_MyWork02.cpp case 3's
    ///     <c>aTribeVoteDate</c> gate replies with Result=1 instead of calling <c>Quit()</c>).
    /// </summary>
    AlreadyVotedThisWindow
}

/// <summary>
///     TRIBE_WORK tSort 52-59's election engine (S04_MyWork02.cpp:11610+, S07_MyGame08.cpp cases 52-59) --
///     the per-tribe candidacy/voting window CZ_TRIBE_VOTE_SEND (opcode 83) gates on, plus the tally that
///     appoints the winning candidate as Force Leader (game.Tribes.MasterCharacterId).
///     <para>
///         Driven automatically by <c>Fenrir.Application.Game.Hosting.World.ZoneWar.TribeVoteElectionCalendarHost</c>,
///         which reads real day/hour and asks <see cref="TribeVoteElectionCalendar" /> (S07_MyGame01.cpp:3530-3630)
///         which of this class's transitions to apply each tick, on whichever instance is configured as server
///         number 37 with <c>VoteTribe</c> enabled. See that calendar class's own remarks for the always-true
///         <c>day &gt;= 1</c> bug it faithfully reproduces: only <see cref="OpenCandidacyWindowAsync" /> is ever
///         automatically reachable in production; <see cref="OpenVotingWindow" />/<see cref="CloseVotingWindow" />/
///         <see cref="AnnounceResultsAsync" />/<see cref="ResetToIdleAsync" /> remain real, correct, directly
///         callable transitions (e.g. for a future GM command) even though the calendar never reaches them itself.
///     </para>
///     <para>
///         GAP: unlike the legacy's own <c>mTribeVoteState</c>, which is part of the durably-persisted WORLD_INFO
///         row (Server/ts25center/S07_MyGame01.cpp:146-156, S08_MyDB.cpp:17-54's <c>GetWorldInfo</c> -- reloaded
///         on every ts25center restart, never reset to idle by a reboot), <see cref="Phase" /> here is
///         in-process memory only and resets to <see cref="TribeVotePhase.Closed" /> on every Fenrir GameServer
///         restart. Wiring it to <c>game.WorldState</c> (a new column alongside <c>TribeSymbolBattle</c>, plus
///         <c>IWorldStateRepository</c>/stored-procedure support) is a fenrir-database-engineer follow-up, out
///         of scope for the Domain/Hosting-only change that introduced the calendar scheduler above -- flagged
///         here rather than silently left inconsistent with the legacy's durability guarantee. The candidate
///         lists and Force Leader/sub-leader appointments this class mutates ARE already fully durable
///         (game.TribeVotes / game.Tribes / game.TribeSubMasters) -- only the phase counter has this gap.
///     </para>
/// </summary>
public sealed class TribeVoteElection(
    WorldStateService worldState,
    ITribeRepository tribes,
    ZoneRegistry zones,
    ILogger<TribeVoteElection> logger)
{
    /// <summary>
    ///     LV_M33 (145, DEFINE.h:483) + MAX_LIMIT_HIGH_LEVEL_NUM (12) + 6 = 163 -- both the candidacy and vote
    ///     eligibility gates in the always-live <c>__GOD__</c> build branch (DEFINE.h:21-30 confirms
    ///     <c>__GOD__</c>/<c>__REBIRTH__</c> are unconditionally defined in every buildable configuration; the
    ///     alternate, ordinary-level-only <c>&lt; LV_M33</c> comparison basis is dead code in every one of
    ///     them). This gate is not a simple ordinary-level threshold: because ordinary level alone caps at 145,
    ///     a character with zero high-level and zero rebirth progress can never satisfy it -- see
    ///     <see cref="CombinedEligibilityLevel" />'s own remarks.
    /// </summary>
    public const int MinimumEligibilityLevel = 163;

    /// <summary>The candidacy CP floor (S04_MyWork02.cpp:11643-11647's <c>aKillOtherTribe &lt; 1000</c> check).</summary>
    public const int MinimumContributionPoints = 1000;

    private readonly Lock _lock = new();
    private readonly HashSet<int> _votedThisWindow = [];

    /// <summary>
    ///     Correlates every log line belonging to one candidacy-&gt;voting-&gt;tally cycle -- minted fresh by
    ///     <see cref="OpenCandidacyWindowAsync" />, carried into every scope below so a single election's whole
    ///     lifecycle can be filtered out of the OTEL log stream by this one id.
    /// </summary>
    private Guid _cycleId;

    private TribeVotePhase _phase = TribeVotePhase.Closed;

    public TribeVotePhase Phase
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
    ///     tSort 52: opens candidacy for every tribe at once (the legacy always moves all 4 tribes in
    ///     lockstep) and wipes any leftover candidate slots from a previous cycle.
    /// </summary>
    public async ValueTask OpenCandidacyWindowAsync(CancellationToken ct)
    {
        _cycleId = Guid.NewGuid();
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", _cycleId);

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            await worldState.ResetTribeVotesAsync(tribeId, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _phase = TribeVotePhase.Candidacy;
            _votedThisWindow.Clear();
        }

        logger.LogInformation(
            "Tribe vote cycle {TribeVoteCycleId} opened candidacy for all {TribeCount} tribes",
            _cycleId, WorldStateService.TribeCount);
    }

    /// <summary>tSort 53: closes candidacy and opens voting for every tribe.</summary>
    public void OpenVotingWindow()
    {
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", _cycleId);

        lock (_lock)
        {
            _phase = TribeVotePhase.Voting;
            _votedThisWindow.Clear();
        }

        logger.LogInformation("Tribe vote cycle {TribeVoteCycleId} opened voting", _cycleId);
    }

    /// <summary>Closes the window without tallying (an aborted cycle) -- both sort 1 and sort 3 reject again.</summary>
    public void CloseWindow()
    {
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", _cycleId);

        lock (_lock)
        {
            _phase = TribeVotePhase.Closed;
        }

        logger.LogInformation("Tribe vote cycle {TribeVoteCycleId} closed", _cycleId);
    }

    /// <summary>
    ///     CZ_TRIBE_VOTE_SEND sort 1 (TRIBE_VOTE_V2 branch): registers <paramref name="player" /> into
    ///     <paramref name="slotIndex" />, displacing whoever currently holds it if their CP is strictly lower.
    /// </summary>
    public async ValueTask<TribeVoteCandidacyOutcome> TryRegisterCandidacyAsync(PlayerRuntimeState player,
        byte slotIndex, CancellationToken ct)
    {
        if (Phase != TribeVotePhase.Candidacy)
            return TribeVoteCandidacyOutcome.WindowClosed;

        if (CombinedEligibilityLevel(player) < MinimumEligibilityLevel)
            return TribeVoteCandidacyOutcome.LevelTooLow;

        if (player.ContributionPoints < MinimumContributionPoints)
            return TribeVoteCandidacyOutcome.NotEnoughContribution;

        var candidates = await worldState.GetTribeVotesAsync(player.Tribe, ct).ConfigureAwait(false);

        foreach (var candidate in candidates)
            if (candidate.CandidateCharacterId == player.CharacterId && candidate.SlotIndex != slotIndex)
                return TribeVoteCandidacyOutcome.AlreadyRegisteredInAnotherSlot;

        foreach (var candidate in candidates)
            if (candidate.SlotIndex == slotIndex && player.ContributionPoints <= candidate.KillOtherTribeCount)
                return TribeVoteCandidacyOutcome.SlotHeldByStrongerCandidate;

        await worldState.RegisterTribeVoteCandidateAsync(player.Tribe, slotIndex, player.CharacterId, player.Level,
            player.ContributionPoints, ct).ConfigureAwait(false);

        return TribeVoteCandidacyOutcome.Registered;
    }

    /// <summary>CZ_TRIBE_VOTE_SEND sort 3: casts one vote for <paramref name="slotIndex" />, once per open window.</summary>
    public async ValueTask<TribeVoteCastOutcome> TryCastVoteAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct)
    {
        if (Phase != TribeVotePhase.Voting)
            return TribeVoteCastOutcome.WindowClosed;

        if (CombinedEligibilityLevel(player) < MinimumEligibilityLevel)
            return TribeVoteCastOutcome.LevelTooLow;

        var candidates = await worldState.GetTribeVotesAsync(player.Tribe, ct).ConfigureAwait(false);

        var slotOccupied = false;
        foreach (var candidate in candidates)
            if (candidate.SlotIndex == slotIndex)
            {
                slotOccupied = true;
                break;
            }

        if (!slotOccupied)
            return TribeVoteCastOutcome.SlotEmpty;

        bool alreadyVoted;
        lock (_lock)
        {
            alreadyVoted = !_votedThisWindow.Add(player.CharacterId);
        }

        if (alreadyVoted)
            return TribeVoteCastOutcome.AlreadyVotedThisWindow;

        // aLevel1 + (aLevel2 + aRebirthNum) * 3 - 112 (S04_MyWork02.cpp:11782).
        var votePoints = player.Level + (player.Level2 + player.RebirthCount) * 3 - 112;

        await worldState.CastTribeVoteAsync(player.Tribe, slotIndex, votePoints, ct).ConfigureAwait(false);

        return TribeVoteCastOutcome.Cast;
    }

    /// <summary>
    ///     tSort 55: appoints tribe <paramref name="tribeId" />'s top VotePoint candidate (ties keep the
    ///     lowest slot index, matching the legacy's own "next must be strictly greater" tally scan) as Force
    ///     Leader, mirroring the role flip onto whichever of the old/new leaders happen to be online right
    ///     now. A tribe with no candidate reaching 1 vote point is left leaderless (returns null), matching the
    ///     legacy's own <c>&lt; 1 continue</c> skip. Does not touch <see cref="Phase" /> -- call
    ///     <see cref="CloseWindow" /> once every tribe has been tallied.
    /// </summary>
    public async ValueTask<int?> TallyForceLeaderAsync(byte tribeId, CancellationToken ct)
    {
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId} Tribe {TribeId}", _cycleId, tribeId);

        var candidates = await worldState.GetTribeVotesAsync(tribeId, ct).ConfigureAwait(false);
        var winner = candidates.Count > 0 && candidates[0].VotePoint >= 1 ? candidates[0] : null;

        var tribeSummaries = await tribes.GetAllAsync(ct).ConfigureAwait(false);
        var previousLeaderId = tribeSummaries.FirstOrDefault(t => t.TribeId == tribeId)?.MasterCharacterId;

        await tribes.SetMasterAsync(tribeId, winner?.CandidateCharacterId, ct).ConfigureAwait(false);

        if (previousLeaderId is { } oldLeaderId && oldLeaderId != winner?.CandidateCharacterId &&
            zones.TryGetPlayerAndZone(oldLeaderId, out _, out var previousZone))
            previousZone.PostTribeProgressCommand(new TribeProgressZoneCommand(oldLeaderId, TribeRole: 0));

        if (winner is not null && zones.TryGetPlayerAndZone(winner.CandidateCharacterId, out _, out var winnerZone))
            winnerZone.PostTribeProgressCommand(new TribeProgressZoneCommand(winner.CandidateCharacterId,
                TribeRole: 1));

        logger.LogInformation(
            "Tribe vote cycle {TribeVoteCycleId} tallied tribe {TribeId}: winner={WinnerCharacterId}",
            _cycleId, tribeId, winner?.CandidateCharacterId);

        return winner?.CandidateCharacterId;
    }

    /// <summary>tSort 54: closes voting for every tribe without tallying -- candidate lists and tallies are untouched.</summary>
    public void CloseVotingWindow()
    {
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", _cycleId);

        lock (_lock)
        {
            _phase = TribeVotePhase.VotingClosed;
        }

        logger.LogInformation("Tribe vote cycle {TribeVoteCycleId} closed voting, awaiting results", _cycleId);
    }

    /// <summary>
    ///     tSort 55: tallies every tribe's Force Leader (see <see cref="TallyForceLeaderAsync" />) and then
    ///     unconditionally wipes every tribe's sub-leader roster -- S04_MyWork02.cpp:507-551's own
    ///     unconditional sub-master clear alongside the elect logic, regardless of whether a tribe actually got
    ///     a new leader out of the tally.
    /// </summary>
    public async ValueTask AnnounceResultsAsync(CancellationToken ct)
    {
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", _cycleId);

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            await TallyForceLeaderAsync(tribeId, ct).ConfigureAwait(false);
            await ClearSubMastersAsync(tribeId, ct).ConfigureAwait(false);
        }

        lock (_lock)
        {
            _phase = TribeVotePhase.ResultsAnnounced;
        }

        logger.LogInformation(
            "Tribe vote cycle {TribeVoteCycleId} announced results for all {TribeCount} tribes",
            _cycleId, WorldStateService.TribeCount);
    }

    /// <summary>
    ///     tSort 56: resets every tribe back to idle, wiping every candidate slot exactly like
    ///     <see cref="OpenCandidacyWindowAsync" /> does for a fresh cycle -- the tail end of the current cycle
    ///     rather than the start of a new one, so this keeps (rather than mints) <see cref="_cycleId" />.
    /// </summary>
    public async ValueTask ResetToIdleAsync(CancellationToken ct)
    {
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", _cycleId);

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            await worldState.ResetTribeVotesAsync(tribeId, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _phase = TribeVotePhase.Closed;
            _votedThisWindow.Clear();
        }

        logger.LogInformation("Tribe vote cycle {TribeVoteCycleId} reset to idle", _cycleId);
    }

    private async ValueTask ClearSubMastersAsync(byte tribeId, CancellationToken ct)
    {
        var subMasters = await tribes.GetSubMastersAsync(tribeId, ct).ConfigureAwait(false);

        foreach (var subMaster in subMasters)
        {
            await tribes.ClearSubMasterAsync(tribeId, subMaster.CharacterId, ct).ConfigureAwait(false);

            if (zones.TryGetPlayerAndZone(subMaster.CharacterId, out _, out var zone))
                zone.PostTribeProgressCommand(new TribeProgressZoneCommand(subMaster.CharacterId, TribeRole: 0));
        }
    }

    /// <summary>
    ///     aLevel1+aLevel2+aRebirthNum -- the three-term sum both <see cref="TryRegisterCandidacyAsync" /> and
    ///     <see cref="TryCastVoteAsync" /> compare against <see cref="MinimumEligibilityLevel" />. Deliberately
    ///     not <see cref="PlayerRuntimeState.CombinedLevel" /> (which omits RebirthCount) -- this gate folds in
    ///     all three terms, unlike the party-invite/trade-ask sites that only need the two-term combined level.
    ///     Because ordinary level alone caps at 145, this gate is unreachable by a character with zero
    ///     high-level and zero rebirth progress no matter how capped their ordinary level is -- a materially
    ///     stricter gate than a simple level threshold, not an incremental addition to one.
    /// </summary>
    private static int CombinedEligibilityLevel(PlayerRuntimeState player)
    {
        return player.Level + player.Level2 + player.RebirthCount;
    }
}
