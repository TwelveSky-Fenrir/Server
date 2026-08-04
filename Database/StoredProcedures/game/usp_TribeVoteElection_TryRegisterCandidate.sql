CREATE PROCEDURE game.usp_TribeVoteElection_TryRegisterCandidate @CycleId UNIQUEIDENTIFIER,
                                                                 @TribeId TINYINT,
                                                                 @SlotIndex TINYINT,
                                                                 @CandidateCharacterId INT,
                                                                 @CandidateLevel SMALLINT,
                                                                 @KillOtherTribeCount INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CandidateLevel < 0
        OR @KillOtherTribeCount < 0
        THROW 50503, 'usp_TribeVoteElection_TryRegisterCandidate requires non-negative candidate statistics.', 1;

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
            THROW 50504, 'usp_TribeVoteElection_TryRegisterCandidate: could not acquire the election lock.', 1;
        END;

    IF NOT EXISTS (SELECT 1
                   FROM game.TribeVoteElectionStates
                   WITH (UPDLOCK, HOLDLOCK)
                   WHERE TribeId = @TribeId
                     AND CycleId = @CycleId
                     AND Phase = 1)
        BEGIN
            COMMIT TRANSACTION;
            SELECT @outcome AS Outcome;
            RETURN;
        END;

    IF EXISTS (SELECT 1
               FROM game.TribeVoteElectionCandidates
               WITH (UPDLOCK, HOLDLOCK)
               WHERE CycleId = @CycleId
                 AND TribeId = @TribeId
                 AND CandidateCharacterId = @CandidateCharacterId
                 AND SlotIndex <> @SlotIndex)
        BEGIN
            SET @outcome = 2;
            COMMIT TRANSACTION;
            SELECT @outcome AS Outcome;
            RETURN;
        END;

    DECLARE @existingKillOtherTribeCount INT = (SELECT KillOtherTribeCount
                                                FROM game.TribeVoteElectionCandidates
                                                WITH (UPDLOCK, HOLDLOCK)
                                                WHERE CycleId = @CycleId
                                                  AND TribeId = @TribeId
                                                  AND SlotIndex = @SlotIndex);

    IF @existingKillOtherTribeCount IS NOT NULL
        AND @KillOtherTribeCount <= @existingKillOtherTribeCount
        BEGIN
            SET @outcome = 3;
            COMMIT TRANSACTION;
            SELECT @outcome AS Outcome;
            RETURN;
        END;

    IF @existingKillOtherTribeCount IS NULL
        INSERT INTO game.TribeVoteElectionCandidates (CycleId, TribeId, SlotIndex, CandidateCharacterId,
                                                      CandidateLevel, KillOtherTribeCount, VotePoint)
        VALUES (@CycleId, @TribeId, @SlotIndex, @CandidateCharacterId, @CandidateLevel,
                @KillOtherTribeCount, 0);
    ELSE
        UPDATE game.TribeVoteElectionCandidates
        SET CandidateCharacterId = @CandidateCharacterId,
            CandidateLevel       = @CandidateLevel,
            KillOtherTribeCount  = @KillOtherTribeCount,
            VotePoint            = 0,
            RegisteredAtUtc      = SYSUTCDATETIME()
        WHERE CycleId = @CycleId
          AND TribeId = @TribeId
          AND SlotIndex = @SlotIndex;

    COMMIT TRANSACTION;
    SELECT CAST(0 AS TINYINT) AS Outcome;
END;
