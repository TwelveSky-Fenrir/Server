using System.Collections.Immutable;
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
    ITribeVoteElectionRepository repository,
    ITribeRepository tribes,
    ZoneRegistry zones,
    ILogger<TribeVoteElection> logger)
{
    public const int MinimumEligibilityLevel = 145;

    public const int MinimumContributionPoints = 1000;

    private const int VoteLevelBaseline = 112;
    private const int TribeCount = WorldStateService.TribeCount;

    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private Guid? _cycleId;

    private ImmutableArray<TribeVoteElectionTribeStateDto> _tribeStates = [];

    public TribeVotePhase Phase => GetPhase(0);

    public Guid? CycleId
    {
        get
        {
            lock (_lock)
            {
                return _cycleId;
            }
        }
    }

    public TribeVotePhase GetPhase(byte tribeId)
    {
        lock (_lock)
        {
            foreach (var state in _tribeStates)
                if (state.TribeId == tribeId)
                    return ToPhase(state.Phase);

            return TribeVotePhase.Closed;
        }
    }

    public async ValueTask LoadDurableSnapshotAsync(CancellationToken ct)
    {
        await _transitionGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await repository.EnsureInitializedAsync(ct).ConfigureAwait(false);
            await ReloadSnapshotAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public async ValueTask OpenCandidacyWindowAsync(CancellationToken ct)
    {
        await _transitionGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var cycleId = Guid.NewGuid();
            await repository.OpenCandidacyAsync(cycleId, ct).ConfigureAwait(false);
            await ReloadSnapshotAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "Tribe vote cycle {TribeVoteCycleId} opened candidacy for all {TribeCount} tribes",
                cycleId, TribeCount);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public async ValueTask<bool> OpenVotingWindowAsync(CancellationToken ct)
    {
        return await TryAdvancePhaseAsync(TribeVotePhase.Candidacy, TribeVotePhase.Voting,
            "opened voting", ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> CloseVotingWindowAsync(CancellationToken ct)
    {
        return await TryAdvancePhaseAsync(TribeVotePhase.Voting, TribeVotePhase.VotingClosed,
            "closed voting, awaiting results", ct).ConfigureAwait(false);
    }

    public async ValueTask<TribeVoteCandidacyOutcome> TryRegisterCandidacyAsync(PlayerRuntimeState player,
        byte slotIndex, CancellationToken ct)
    {
        if (GetPhase(player.Tribe) != TribeVotePhase.Candidacy)
            return TribeVoteCandidacyOutcome.WindowClosed;

        if (CombinedEligibilityLevel(player) < MinimumEligibilityLevel)
            return TribeVoteCandidacyOutcome.LevelTooLow;

        if (player.ContributionPoints < MinimumContributionPoints)
            return TribeVoteCandidacyOutcome.NotEnoughContribution;

        var cycleId = CycleId;
        if (cycleId is null)
            return TribeVoteCandidacyOutcome.WindowClosed;

        var candidateLevel = (short)(player.CombinedLevel - VoteLevelBaseline);
        var persistenceOutcome = await repository.TryRegisterCandidateAsync(cycleId.Value, player.Tribe, slotIndex,
            player.CharacterId, candidateLevel, player.ContributionPoints, ct).ConfigureAwait(false);

        return persistenceOutcome switch
        {
            TribeVoteElectionCandidateRegistrationOutcome.Registered => TribeVoteCandidacyOutcome.Registered,
            TribeVoteElectionCandidateRegistrationOutcome.WindowClosed => TribeVoteCandidacyOutcome.WindowClosed,
            TribeVoteElectionCandidateRegistrationOutcome.AlreadyRegisteredInAnotherSlot =>
                TribeVoteCandidacyOutcome.AlreadyRegisteredInAnotherSlot,
            TribeVoteElectionCandidateRegistrationOutcome.SlotHeldByStrongerCandidate =>
                TribeVoteCandidacyOutcome.SlotHeldByStrongerCandidate,
            _ => throw new ArgumentOutOfRangeException(nameof(persistenceOutcome), persistenceOutcome,
                "Unknown durable tribe-vote candidacy outcome.")
        };
    }

    public async ValueTask<TribeVoteCastOutcome> TryCastVoteAsync(PlayerRuntimeState player, byte slotIndex,
        CancellationToken ct)
    {
        if (GetPhase(player.Tribe) != TribeVotePhase.Voting)
            return TribeVoteCastOutcome.WindowClosed;

        if (player.Level < MinimumEligibilityLevel)
            return TribeVoteCastOutcome.LevelTooLow;

        var cycleId = CycleId;
        if (cycleId is null)
            return TribeVoteCastOutcome.WindowClosed;

        var votePoints = player.Level + (player.Level2 + player.RebirthCount) * 3 - VoteLevelBaseline;
        var persistenceOutcome = await repository.TryCastVoteAsync(cycleId.Value, player.CharacterId, player.Tribe,
            slotIndex, votePoints, ct).ConfigureAwait(false);

        return persistenceOutcome switch
        {
            TribeVoteElectionVoteCastOutcome.Cast => TribeVoteCastOutcome.Cast,
            TribeVoteElectionVoteCastOutcome.WindowClosed => TribeVoteCastOutcome.WindowClosed,
            TribeVoteElectionVoteCastOutcome.SlotEmpty => TribeVoteCastOutcome.SlotEmpty,
            TribeVoteElectionVoteCastOutcome.AlreadyVotedThisCycle => TribeVoteCastOutcome.AlreadyVotedThisWindow,
            _ => throw new ArgumentOutOfRangeException(nameof(persistenceOutcome), persistenceOutcome,
                "Unknown durable tribe-vote casting outcome.")
        };
    }

    public async ValueTask<int?> TallyForceLeaderAsync(byte tribeId, CancellationToken ct)
    {
        var snapshot = await repository.LoadSnapshotAsync(ct).ConfigureAwait(false);
        return await TallyForceLeaderAsync(tribeId, snapshot.Candidates, ct).ConfigureAwait(false);
    }

    public async ValueTask AnnounceResultsAsync(CancellationToken ct)
    {
        await _transitionGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var snapshot = await ReloadSnapshotAsync(ct).ConfigureAwait(false);
            var cycleId = GetCurrentCycleId(snapshot);
            if (cycleId is null || !HasPhase(snapshot, TribeVotePhase.VotingClosed))
                return;

            using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", cycleId);

            for (byte tribeId = 0; tribeId < TribeCount; tribeId++)
            {
                await TallyForceLeaderAsync(tribeId, snapshot.Candidates, ct).ConfigureAwait(false);
                await ClearSubMastersAsync(tribeId, ct).ConfigureAwait(false);
            }

            if (!await repository.TryAdvancePhaseAsync(cycleId.Value, (byte)TribeVotePhase.VotingClosed,
                    (byte)TribeVotePhase.ResultsAnnounced, ct).ConfigureAwait(false))
                return;

            await ReloadSnapshotAsync(ct).ConfigureAwait(false);

            logger.LogInformation(
                "Tribe vote cycle {TribeVoteCycleId} announced results for all {TribeCount} tribes",
                cycleId, TribeCount);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public async ValueTask ResetToIdleAsync(CancellationToken ct)
    {
        await _transitionGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var cycleId = CycleId;
            using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", cycleId);

            await repository.ResetToIdleAsync(ct).ConfigureAwait(false);
            await ReloadSnapshotAsync(ct).ConfigureAwait(false);

            logger.LogInformation("Tribe vote cycle {TribeVoteCycleId} reset to idle", cycleId);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    private async ValueTask<bool> TryAdvancePhaseAsync(TribeVotePhase expectedPhase, TribeVotePhase nextPhase,
        string action, CancellationToken ct)
    {
        await _transitionGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var cycleId = CycleId;
            if (cycleId is null || !HasPhase(expectedPhase))
                return false;

            using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId}", cycleId);

            if (!await repository.TryAdvancePhaseAsync(cycleId.Value, (byte)expectedPhase, (byte)nextPhase, ct)
                    .ConfigureAwait(false))
            {
                await ReloadSnapshotAsync(ct).ConfigureAwait(false);
                return false;
            }

            await ReloadSnapshotAsync(ct).ConfigureAwait(false);
            logger.LogInformation("Tribe vote cycle {TribeVoteCycleId} {Action}", cycleId, action);
            return true;
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    private async ValueTask<int?> TallyForceLeaderAsync(byte tribeId,
        ImmutableArray<TribeVoteElectionCandidateDto> candidates, CancellationToken ct)
    {
        var winner = TribeVoteElectionTally.SelectWinner(candidates, tribeId);
        var cycleId = CycleId;
        using var scope = logger.BeginScope("TribeVoteCycle {TribeVoteCycleId} Tribe {TribeId}", cycleId, tribeId);

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
            cycleId, tribeId, winner?.CandidateCharacterId);

        return winner?.CandidateCharacterId;
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

    private async ValueTask<TribeVoteElectionSnapshot> ReloadSnapshotAsync(CancellationToken ct)
    {
        var snapshot = await repository.LoadSnapshotAsync(ct).ConfigureAwait(false);
        ApplySnapshot(snapshot);
        return snapshot;
    }

    private void ApplySnapshot(TribeVoteElectionSnapshot snapshot)
    {
        if (snapshot.TribeStates.Length != TribeCount)
            throw new InvalidOperationException(
                $"Durable tribe-vote election snapshot must contain {TribeCount} tribe states.");

        var orderedStates = snapshot.TribeStates.OrderBy(static state => state.TribeId).ToImmutableArray();
        for (byte tribeId = 0; tribeId < TribeCount; tribeId++)
        {
            var state = orderedStates[tribeId];
            if (state.TribeId != tribeId)
                throw new InvalidOperationException(
                    "Durable tribe-vote election snapshot has non-canonical tribe ids.");

            _ = ToPhase(state.Phase);
        }

        var cycleId = orderedStates[0].CycleId;
        if (orderedStates.Any(state => state.CycleId != cycleId))
            throw new InvalidOperationException(
                "Durable tribe-vote election snapshot has inconsistent cycle identities.");

        foreach (var candidate in snapshot.Candidates)
            if (candidate.TribeId >= TribeCount || candidate.CycleId != cycleId || candidate.VotePoint < 0)
                throw new InvalidOperationException(
                    "Durable tribe-vote election snapshot has an invalid candidate tally.");

        lock (_lock)
        {
            _tribeStates = orderedStates;
            _cycleId = cycleId;
        }
    }

    private bool HasPhase(TribeVotePhase phase)
    {
        lock (_lock)
        {
            return _tribeStates.Length == TribeCount && _tribeStates.All(state => ToPhase(state.Phase) == phase);
        }
    }

    private static bool HasPhase(TribeVoteElectionSnapshot snapshot, TribeVotePhase phase)
    {
        return snapshot.TribeStates.Length == TribeCount &&
               snapshot.TribeStates.All(state => ToPhase(state.Phase) == phase);
    }

    private static Guid? GetCurrentCycleId(TribeVoteElectionSnapshot snapshot)
    {
        return snapshot.TribeStates.Length == TribeCount &&
               snapshot.TribeStates.All(state => state.CycleId == snapshot.TribeStates[0].CycleId)
            ? snapshot.TribeStates[0].CycleId
            : null;
    }

    private static TribeVotePhase ToPhase(byte phase)
    {
        return phase switch
        {
            (byte)TribeVotePhase.Closed => TribeVotePhase.Closed,
            (byte)TribeVotePhase.Candidacy => TribeVotePhase.Candidacy,
            (byte)TribeVotePhase.Voting => TribeVotePhase.Voting,
            (byte)TribeVotePhase.VotingClosed => TribeVotePhase.VotingClosed,
            (byte)TribeVotePhase.ResultsAnnounced => TribeVotePhase.ResultsAnnounced,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase,
                "Unknown durable tribe-vote election phase.")
        };
    }

    private static int CombinedEligibilityLevel(PlayerRuntimeState player)
    {
        return player.Level + player.Level2 + player.RebirthCount;
    }
}
