-- database/50_procedures/game/usp_HeroRanking_Upsert.sql
-- Contract: create or update one character's ranking row for a given period.
-- Params:
--   @CharacterId   INT
--   @PeriodKind    TINYINT       -- 0 = Current, 1 = Previous
--   @Points        INT
--   @TribeId       TINYINT NULL  -- game.Tribes.TribeId
--   @Level         INT     NULL
--   @RewardClaimed BIT     NULL
--   @Description   NVARCHAR(255) NULL
-- Result set: none.
-- Idempotent: yes -- re-running with the same inputs leaves the row in the same state (RecordedAtUtc is
-- refreshed each call, same as game.usp_GuildNotice_Set's own idiom).
-- No MERGE (forbidden, architecture reference §12.3): a single guarded UPDATE, falling back to INSERT only
-- when no row was touched.
CREATE PROCEDURE game.usp_HeroRanking_Upsert @CharacterId   INT,
    @PeriodKind    TINYINT,
    @Points        INT,
    @TribeId       TINYINT = NULL,
    @Level         INT = NULL,
    @RewardClaimed BIT = NULL,
    @Description   NVARCHAR(255) = NULL
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.HeroRankings
SET Points        = @Points,
    TribeId       = @TribeId,
    Level         = @Level,
    RewardClaimed = @RewardClaimed,
    Description   = @Description,
    RecordedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @CharacterId
  AND PeriodKind = @PeriodKind;

IF
@@ROWCOUNT = 0
        INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RewardClaimed, Description, RecordedAtUtc)
        VALUES (@CharacterId, @PeriodKind, @Points, @TribeId, @Level, @RewardClaimed, @Description, SYSUTCDATETIME());
END;
