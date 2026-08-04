CREATE PROCEDURE game.usp_TribeVoteElection_LoadSnapshot
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @lockResult INT;
    EXEC @lockResult = sp_getapplock
                       @Resource = 'game.TribeVoteElection',
                       @LockMode = 'Shared',
                       @LockOwner = 'Transaction',
                       @LockTimeout = 30000;

    IF @lockResult < 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50496, 'usp_TribeVoteElection_LoadSnapshot: could not acquire the election lock.', 1;
        END;

    SELECT TribeId,
           CycleId,
           Phase,
           UpdatedAtUtc
    FROM game.TribeVoteElectionStates
    ORDER BY TribeId;

    SELECT candidate.CycleId,
           candidate.TribeId,
           candidate.SlotIndex,
           candidate.CandidateCharacterId,
           candidate.CandidateLevel,
           candidate.KillOtherTribeCount,
           candidate.VotePoint,
           candidate.RegisteredAtUtc
    FROM game.TribeVoteElectionCandidates AS candidate
             INNER JOIN game.TribeVoteElectionStates AS state
                        ON state.TribeId = candidate.TribeId
                            AND state.CycleId = candidate.CycleId
    ORDER BY candidate.TribeId, candidate.SlotIndex;

    COMMIT TRANSACTION;
END;
