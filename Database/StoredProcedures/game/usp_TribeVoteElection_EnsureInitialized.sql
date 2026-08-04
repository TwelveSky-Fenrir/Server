CREATE PROCEDURE game.usp_TribeVoteElection_EnsureInitialized
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
            THROW 50490, 'usp_TribeVoteElection_EnsureInitialized: could not acquire the election lock.', 1;
        END;

    INSERT INTO game.TribeVoteElectionStates (TribeId)
    SELECT tribe.TribeId
    FROM game.Tribes AS tribe
    WHERE tribe.TribeId BETWEEN 0 AND 3
      AND NOT EXISTS (SELECT 1
                      FROM game.TribeVoteElectionStates AS state
                      WITH (UPDLOCK, HOLDLOCK)
                      WHERE state.TribeId = tribe.TribeId);

    IF (SELECT COUNT(*) FROM game.TribeVoteElectionStates WHERE TribeId BETWEEN 0 AND 3) <> 4
        THROW 50491, 'usp_TribeVoteElection_EnsureInitialized requires canonical tribe rows 0 through 3.', 1;

    COMMIT TRANSACTION;
END;
