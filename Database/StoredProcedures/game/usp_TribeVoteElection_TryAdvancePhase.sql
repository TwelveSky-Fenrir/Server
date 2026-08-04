CREATE PROCEDURE game.usp_TribeVoteElection_TryAdvancePhase @CycleId UNIQUEIDENTIFIER,
                                                            @ExpectedPhase TINYINT,
                                                            @NextPhase TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ExpectedPhase NOT BETWEEN 1 AND 3
       OR @NextPhase <> @ExpectedPhase + 1
        THROW 50498, 'usp_TribeVoteElection_TryAdvancePhase requires the next legal phase transition.', 1;

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
            THROW 50499, 'usp_TribeVoteElection_TryAdvancePhase: could not acquire the election lock.', 1;
        END;

    DECLARE @matchingStateCount INT = (SELECT COUNT(*)
                                       FROM game.TribeVoteElectionStates WITH (UPDLOCK, HOLDLOCK)
                                       WHERE CycleId = @CycleId
                                         AND Phase = @ExpectedPhase);

    IF @matchingStateCount <> 4
        BEGIN
            COMMIT TRANSACTION;
            SELECT CAST(0 AS BIT) AS Advanced;
            RETURN;
        END;

    UPDATE game.TribeVoteElectionStates
    SET Phase = @NextPhase,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CycleId = @CycleId
      AND Phase = @ExpectedPhase;

    COMMIT TRANSACTION;
    SELECT CAST(1 AS BIT) AS Advanced;
END;
