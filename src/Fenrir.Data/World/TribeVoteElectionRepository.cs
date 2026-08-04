using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Data.World;

public sealed record TribeVoteElectionRepository(ICaeriusNetDbContext Db) : ITribeVoteElectionRepository
{
    private const int TribeStateResultSetCapacity = 4;
    private const int CandidateResultSetCapacity = 40;

    public async ValueTask EnsureInitializedAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVoteElection_EnsureInitialized", 0).Build();
        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<TribeVoteElectionSnapshot> LoadSnapshotAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVoteElection_LoadSnapshot",
                TribeStateResultSetCapacity + CandidateResultSetCapacity)
            .Build();

        var (tribeStates, candidates) = await Db
            .QueryMultipleImmutableArrayAsync<TribeVoteElectionTribeStateDto, TribeVoteElectionCandidateDto>(sp, ct);

        return new TribeVoteElectionSnapshot(tribeStates, candidates);
    }

    public async ValueTask OpenCandidacyAsync(Guid cycleId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVoteElection_OpenCandidacy", 0)
            .AddParameter("CycleId", cycleId, SqlDbType.UniqueIdentifier)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<bool> TryAdvancePhaseAsync(Guid cycleId, byte expectedPhase, byte nextPhase,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVoteElection_TryAdvancePhase", 1)
            .AddParameter("CycleId", cycleId, SqlDbType.UniqueIdentifier)
            .AddParameter("ExpectedPhase", expectedPhase, SqlDbType.TinyInt)
            .AddParameter("NextPhase", nextPhase, SqlDbType.TinyInt)
            .Build();

        return await Db.ExecuteScalarAsync<bool>(sp, ct);
    }

    public async ValueTask<TribeVoteElectionCandidateRegistrationOutcome> TryRegisterCandidateAsync(Guid cycleId,
        byte tribeId, byte slotIndex, int candidateCharacterId, short candidateLevel, int killOtherTribeCount,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVoteElection_TryRegisterCandidate", 1)
            .AddParameter("CycleId", cycleId, SqlDbType.UniqueIdentifier)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("CandidateCharacterId", candidateCharacterId, SqlDbType.Int)
            .AddParameter("CandidateLevel", candidateLevel, SqlDbType.SmallInt)
            .AddParameter("KillOtherTribeCount", killOtherTribeCount, SqlDbType.Int)
            .Build();

        var outcome = await Db.ExecuteScalarAsync<byte>(sp, ct);
        return (TribeVoteElectionCandidateRegistrationOutcome)outcome;
    }

    public async ValueTask<TribeVoteElectionVoteCastOutcome> TryCastVoteAsync(Guid cycleId, int voterCharacterId,
        byte tribeId, byte slotIndex, int points, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVoteElection_TryCastVote", 1)
            .AddParameter("CycleId", cycleId, SqlDbType.UniqueIdentifier)
            .AddParameter("VoterCharacterId", voterCharacterId, SqlDbType.Int)
            .AddParameter("TribeId", tribeId, SqlDbType.TinyInt)
            .AddParameter("SlotIndex", slotIndex, SqlDbType.TinyInt)
            .AddParameter("Points", points, SqlDbType.Int)
            .Build();

        var outcome = await Db.ExecuteScalarAsync<byte>(sp, ct);
        return (TribeVoteElectionVoteCastOutcome)outcome;
    }

    public async ValueTask ResetToIdleAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TribeVoteElection_ResetToIdle", 0).Build();
        await Db.ExecuteAsync(sp, ct);
    }
}
