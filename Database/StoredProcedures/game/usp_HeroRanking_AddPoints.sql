CREATE PROCEDURE game.usp_HeroRanking_AddPoints @CharacterId INT,
                                                @PeriodKind TINYINT,
                                                @Delta INT,
                                                @TribeId TINYINT = NULL,
                                                @Level SMALLINT = NULL
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
        INSERT INTO game.HeroRankings (CharacterId, PeriodKind, Points, TribeId, Level, RecordedAtUtc)
        VALUES (@CharacterId, @PeriodKind, @Delta, @TribeId, @Level, SYSUTCDATETIME());
    ELSE
        UPDATE game.HeroRankings
        SET Points        = Points + @Delta,
            TribeId       = COALESCE(@TribeId, TribeId),
            RecordedAtUtc = SYSUTCDATETIME()
        WHERE CharacterId = @CharacterId
          AND PeriodKind = @PeriodKind;

    COMMIT TRANSACTION;

    SELECT Points
    FROM game.HeroRankings
    WHERE CharacterId = @CharacterId
      AND PeriodKind = @PeriodKind;
END;
