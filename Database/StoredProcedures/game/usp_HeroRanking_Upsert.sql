-- PeriodKind: 0 = Current, 1 = Previous.
--
-- Concurrency fix (2026-07-12 hardening pass, closing the finding previously flagged-not-fixed in
-- usp_HeroRanking_AddPoints.sql's own header comment): this proc had the same unguarded
-- "UPDATE; IF @@ROWCOUNT = 0 INSERT" shape with no explicit transaction. Its only current caller
-- (HeroRankingRepository.MarkRewardClaimedAsync) fires against a row usp_HeroRanking_Rollover already
-- created, so the INSERT branch is practically dead code for that call site -- the guard below is added
-- anyway for schema-wide consistency with the sibling fixes in usp_HeroRanking_AddPoints.sql/
-- usp_TribeVote_Register.sql, and to close the residual same-character double-claim-from-two-sessions
-- race outright rather than merely documenting it as low-probability. Same idiom: BEGIN TRANSACTION
-- wrapping a WITH (UPDLOCK, HOLDLOCK) existence check on the exact PK_HeroRankings (CharacterId,
-- PeriodKind) key, so only concurrent callers for the SAME key ever serialize.
CREATE PROCEDURE game.usp_HeroRanking_Upsert @CharacterId INT,
                                             @PeriodKind TINYINT,
                                             @Points INT,
                                             @TribeId TINYINT = NULL,
                                             @Level SMALLINT = NULL,
                                             @RewardClaimed BIT = NULL,
                                             @Description NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1
                   FROM game.HeroRankings WITH (UPDLOCK, HOLDLOCK)
                   WHERE CharacterId = @CharacterId
                     AND PeriodKind = @PeriodKind)
        INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RewardClaimed, Description,
                                       RecordedAtUtc)
        VALUES (@CharacterId, @PeriodKind, @Points, @TribeId, @Level, @RewardClaimed, @Description, SYSUTCDATETIME());
    ELSE
        UPDATE game.HeroRankings
        SET Points        = @Points,
            TribeId       = @TribeId,
            Level         = @Level,
            RewardClaimed = @RewardClaimed,
            Description   = @Description,
            RecordedAtUtc = SYSUTCDATETIME()
        WHERE CharacterId = @CharacterId
          AND PeriodKind = @PeriodKind;

    COMMIT TRANSACTION;
END;
