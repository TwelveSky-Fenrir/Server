using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>The per-cycle Force Leader election window, mirroring legacy <c>mWorldInfo-&gt;mTribeVoteState</c>.</summary>
public enum TribeVotePhase : byte
{
    /// <summary>Default/reset state (legacy 0) -- CZ_TRIBE_VOTE_SEND sort 1 and 3 both reject.</summary>
    Closed = 0,

    /// <summary>Legacy 1 -- sort 1 (candidacy) accepted, sort 3 (vote) rejected.</summary>
    Candidacy = 1,

    /// <summary>Legacy 2 -- sort 3 (vote) accepted, sort 1 (candidacy) rejected.</summary>
    Voting = 2
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
///         The phase itself is intentionally process-local/ephemeral, matching the legacy's own shared-memory
///         <c>mTribeVoteState</c> (never a persisted column): nothing in Fenrir yet schedules when a cycle
///         opens/closes (same class of gap as <see cref="Progression.TowerWarState" />'s tower-siege trigger),
///         so <see cref="OpenCandidacyWindowAsync" />/<see cref="OpenVotingWindow" />/
///         <see cref="TallyForceLeaderAsync" /> are real, production-ready API waiting for whichever future
///         GM-command/scheduled-job surface decides to drive a cycle -- the register/vote/tally mechanics
///         underneath are already fully functional today.
///     </para>
/// </summary>
public sealed class TribeVoteElection(
    WorldStateService worldState,
    ITribeRepository tribes,
    ZoneRegistry zones,
    ILogger<TribeVoteElection> logger)
{
    /// <summary>LV_M33 (DEFINE.h:483) -- both the candidacy and vote level gates in the active (non-__GOD__) build.</summary>
    public const short MinimumLevel = 145;

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

        if (player.Level < MinimumLevel)
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

        if (player.Level < MinimumLevel)
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

        // aLevel1 + (aLevel2 + aRebirthNum) * 3 - 112 (S04_MyWork02.cpp:11782); Fenrir has no separate
        // aLevel2 track, so Level stands in for aLevel1 alone here -- documented simplification.
        var votePoints = player.Level + player.RebirthCount * 3 - 112;

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
}
