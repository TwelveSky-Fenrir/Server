CREATE PROCEDURE game.usp_TribeVote_Register @TribeId TINYINT,
                                             @SlotIndex TINYINT,
                                             @CandidateCharacterId INT,
                                             @CandidateLevel SMALLINT,
                                             @KillOtherTribeCount INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF
        EXISTS (SELECT 1
                FROM game.TribeVotes
                WITH (UPDLOCK, HOLDLOCK)
                WHERE TribeId = @TribeId
                  AND SlotIndex = @SlotIndex)
        UPDATE game.TribeVotes
        SET CandidateCharacterId = @CandidateCharacterId,
            CandidateLevel       = @CandidateLevel,
            KillOtherTribeCount  = @KillOtherTribeCount,
            VotePoint            = 0,
            RegisteredAtUtc      = SYSUTCDATETIME()
        WHERE TribeId = @TribeId
          AND SlotIndex = @SlotIndex;
    ELSE
        INSERT INTO game.TribeVotes (TribeId, SlotIndex, CandidateCharacterId, CandidateLevel, KillOtherTribeCount,
                                     VotePoint)
        VALUES (@TribeId, @SlotIndex, @CandidateCharacterId, @CandidateLevel, @KillOtherTribeCount, 0);

    COMMIT TRANSACTION;
END;
