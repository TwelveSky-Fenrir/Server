CREATE PROCEDURE game.usp_TribeVoteElection_OpenCandidacy @CycleId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CycleId IS NULL
        THROW 50497, 'usp_TribeVoteElection_OpenCandidacy requires a cycle identifier.', 1;

    BEGIN TRANSACTION;

    DECLARE @lockResult INT;
    EXEC @lockResult = sp_getapplock
                       @Resource = 'game.TribeVoteElection',
                       @LockMode = 'Exclusive',
                       @LockOwner = 'Transaction',
                       @LockTimeout = 30000;

    IF @lockResult < 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50492, 'usp_TribeVoteElection_OpenCandidacy: could not acquire the election lock.', 1;
        END;

    DELETE FROM game.TribeVoteElectionVoters;
    DELETE FROM game.TribeVoteElectionCandidates;

    UPDATE game.TribeVoteElectionStates
    SET CycleId = @CycleId,
        Phase = 1,
        UpdatedAtUtc = SYSUTCDATETIME();

    IF @@ROWCOUNT <> 4
        THROW 50493, 'usp_TribeVoteElection_OpenCandidacy requires four durable tribe-election state rows.', 1;

    COMMIT TRANSACTION;
END;
