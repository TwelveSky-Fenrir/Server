-- PeriodKind: 0 = Current, 1 = Previous.
CREATE PROCEDURE game.usp_HeroRanking_Upsert @CharacterId INT,
                                             @PeriodKind TINYINT,
                                             @Points INT,
                                             @TribeId TINYINT = NULL,
                                             @Level INT = NULL,
                                             @RewardClaimed BIT = NULL,
                                             @Description NVARCHAR(255) = NULL
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
        INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RewardClaimed, Description,
                                       RecordedAtUtc)
        VALUES (@CharacterId, @PeriodKind, @Points, @TribeId, @Level, @RewardClaimed, @Description, SYSUTCDATETIME());
END;
