CREATE PROCEDURE game.usp_HeroRanking_GetPoints @CharacterId INT,
                                                @PeriodKind TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Points
    FROM game.HeroRankings
    WHERE CharacterId = @CharacterId
      AND PeriodKind = @PeriodKind;
END;
