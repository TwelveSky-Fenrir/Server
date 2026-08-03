CREATE PROCEDURE game.usp_HeroRanking_GetByPeriod @PeriodKind TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;

    IF
        @PeriodKind = 0
        SELECT CharacterId,
               CharacterName,
               TribeId,
               Points,
               Level,
               RewardClaimed,
               Description,
               RecordedAtUtc
        FROM game.vw_HeroRankingCurrent
        ORDER BY Points DESC;
    ELSE
        SELECT r.CharacterId,
               c.Name AS CharacterName,
               r.TribeId,
               r.Points,
               r.Level,
               r.RewardClaimed,
               r.Description,
               r.RecordedAtUtc
        FROM game.HeroRankings r
                 JOIN game.Characters c ON c.CharacterId = r.CharacterId
        WHERE r.PeriodKind = @PeriodKind
        ORDER BY r.Points DESC;
END;
