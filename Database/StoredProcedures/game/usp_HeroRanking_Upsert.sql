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
                   FROM game.HeroRankings
                   WITH (UPDLOCK, HOLDLOCK)
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
