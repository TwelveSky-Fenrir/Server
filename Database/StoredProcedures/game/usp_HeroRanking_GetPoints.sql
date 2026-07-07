-- hero-rank-points-world-entry-hydration: single-character/period point-total read used by EnterWorldService
-- to hydrate PlayerRuntimeState.HeroRankPoints at world entry from the character's already-persisted
-- Current-period total (see Server/ts25login/S08_MyDB.cpp:1178-1188's GetHeroPoint and
-- Server/ts25playuser/S07_MyGame01.cpp:1002). game.HeroRankings already has a natural (CharacterId,
-- PeriodKind) primary key, so at most one row can ever match. No accumulated row yet (character never earned
-- a point) is a legitimate zero, indistinguishable here from any other empty-result case.
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
