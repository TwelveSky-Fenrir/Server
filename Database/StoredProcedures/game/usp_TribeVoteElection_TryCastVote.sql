CREATE PROCEDURE game.usp_TribeVoteElection_TryCastVote @CycleId UNIQUEIDENTIFIER,
                                                        @VoterCharacterId INT,
                                                        @TribeId TINYINT,
                                                        @SlotIndex TINYINT,
                                                        @Points INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Points <= 0
        THROW 50500, 'usp_TribeVoteElection_TryCastVote requires positive vote points.', 1;

    DECLARE @outcome TINYINT = 1;

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
            THROW 50501, 'usp_TribeVoteElection_TryCastVote: could not acquire the election lock.', 1;
        END;

    IF NOT EXISTS (SELECT 1
                   FROM game.TribeVoteElectionStates WITH (UPDLOCK, HOLDLOCK)
                   WHERE TribeId = @TribeId
                     AND CycleId = @CycleId
                     AND Phase = 2)
        BEGIN
            COMMIT TRANSACTION;
            SELECT @outcome AS Outcome;
            RETURN;
        END;

    IF NOT EXISTS (SELECT 1
                   FROM game.TribeVoteElectionCandidates WITH (UPDLOCK, HOLDLOCK)
                   WHERE CycleId = @CycleId
                     AND TribeId = @TribeId
                     AND SlotIndex = @SlotIndex)
        BEGIN
            SET @outcome = 2;
            COMMIT TRANSACTION;
            SELECT @outcome AS Outcome;
            RETURN;
        END;

    IF EXISTS (SELECT 1
               FROM game.TribeVoteElectionVoters WITH (UPDLOCK, HOLDLOCK)
               WHERE CycleId = @CycleId
                 AND VoterCharacterId = @VoterCharacterId)
        BEGIN
            SET @outcome = 3;
            COMMIT TRANSACTION;
            SELECT @outcome AS Outcome;
            RETURN;
        END;

    IF EXISTS (SELECT 1
               FROM game.TribeVoteElectionCandidates WITH (UPDLOCK, HOLDLOCK)
               WHERE CycleId = @CycleId
                 AND TribeId = @TribeId
                 AND SlotIndex = @SlotIndex
                 AND VotePoint > 2147483647 - @Points)
        THROW 50502, 'usp_TribeVoteElection_TryCastVote would overflow the candidate vote tally.', 1;

    INSERT INTO game.TribeVoteElectionVoters (CycleId, VoterCharacterId, TribeId, SlotIndex, VotePoints)
    VALUES (@CycleId, @VoterCharacterId, @TribeId, @SlotIndex, @Points);

    UPDATE game.TribeVoteElectionCandidates
    SET VotePoint = VotePoint + @Points
    WHERE CycleId = @CycleId
      AND TribeId = @TribeId
      AND SlotIndex = @SlotIndex;

    COMMIT TRANSACTION;
    SELECT CAST(0 AS TINYINT) AS Outcome;
END;
