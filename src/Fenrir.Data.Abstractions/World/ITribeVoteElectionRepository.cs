namespace Fenrir.Data.Abstractions.World;

public enum TribeVoteElectionCandidateRegistrationOutcome : byte
{
    Registered,
    WindowClosed,
    AlreadyRegisteredInAnotherSlot,
    SlotHeldByStrongerCandidate
}

public enum TribeVoteElectionVoteCastOutcome : byte
{
    Cast,
    WindowClosed,
    SlotEmpty,
    AlreadyVotedThisCycle
}

public interface ITribeVoteElectionRepository
{
    public ValueTask EnsureInitializedAsync(CancellationToken ct);

    public ValueTask<TribeVoteElectionSnapshot> LoadSnapshotAsync(CancellationToken ct);

    public ValueTask OpenCandidacyAsync(Guid cycleId, CancellationToken ct);

    public ValueTask<bool> TryAdvancePhaseAsync(Guid cycleId, byte expectedPhase, byte nextPhase,
        CancellationToken ct);

    public ValueTask<TribeVoteElectionCandidateRegistrationOutcome> TryRegisterCandidateAsync(Guid cycleId,
        byte tribeId, byte slotIndex, int candidateCharacterId, short candidateLevel, int killOtherTribeCount,
        CancellationToken ct);

    public ValueTask<TribeVoteElectionVoteCastOutcome> TryCastVoteAsync(Guid cycleId, int voterCharacterId,
        byte tribeId, byte slotIndex, int points, CancellationToken ct);

    public ValueTask ResetToIdleAsync(CancellationToken ct);
}
