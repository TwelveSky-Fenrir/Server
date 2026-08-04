CREATE PROCEDURE game.usp_TribeVoteElection_ResetToIdle
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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
            THROW 50494, 'usp_TribeVoteElection_ResetToIdle: could not acquire the election lock.', 1;
        END;

    DELETE FROM game.TribeVoteElectionVoters;
    DELETE FROM game.TribeVoteElectionCandidates;

    UPDATE game.TribeVoteElectionStates
    SET CycleId      = NULL,
        Phase        = 0,
        UpdatedAtUtc = SYSUTCDATETIME();

    IF @@ROWCOUNT <> 4
        THROW 50495, 'usp_TribeVoteElection_ResetToIdle requires four durable tribe-election state rows.', 1;

    COMMIT TRANSACTION;
END;
