using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum TribeVotePhase : byte
{
    Closed = 0,

    Candidacy = 1,

    Voting = 2,

    VotingClosed = 3,

    ResultsAnnounced = 4
}

public enum TribeVoteCandidacyOutcome
{
    Registered,
    WindowClosed,
    LevelTooLow,
    NotEnoughContribution,
    AlreadyRegisteredInAnotherSlot,
    SlotHeldByStrongerCandidate
}

public enum TribeVoteCastOutcome
{
    Cast,
    WindowClosed,
    LevelTooLow,
    SlotEmpty,

    AlreadyVotedThisWindow
}

public sealed class TribeVoteElection(
    WorldStateService worldState,
    ITribeRepository tribes,
    ZoneRegistry zones,
    ILogger<TribeVoteElection> logger)
{
    public const int MinimumEligibilityLevel = 163;

    public const int MinimumContributionPoints = 1000;

    private readonly Lock _lock = new();
    private readonly HashSet<int> _votedThisWindow = [];

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

    public void CloseWindow()
    {
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", _cycleId);

        lock (_lock)
        {
            _phase = TribeVotePhase.Closed;
        }

        logger.LogInformation("Tribe vote cycle {TribeVoteCycleId} closed", _cycleId);
    }

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

        var votePoints = player.Level + (player.Level2 + player.RebirthCount) * 3 - 112;

        await worldState.CastTribeVoteAsync(player.Tribe, slotIndex, votePoints, ct).ConfigureAwait(false);

        return TribeVoteCastOutcome.Cast;
    }

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

    public void CloseVotingWindow()
    {
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", _cycleId);

        lock (_lock)
        {
            _phase = TribeVotePhase.VotingClosed;
        }

        logger.LogInformation("Tribe vote cycle {TribeVoteCycleId} closed voting, awaiting results", _cycleId);
    }

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

    private static int CombinedEligibilityLevel(PlayerRuntimeState player)
    {
        return player.Level + player.Level2 + player.RebirthCount;
    }
}
