-- database/50_procedures/game/usp_HeroRanking_GetByPeriod.sql
-- Contract: @PeriodKind (0=Current, 1=Previous) -> RS0 { CharacterId, CharacterName, TribeId, Points,
--           Level, RewardClaimed, Description, RecordedAtUtc }, one row per ranked character, ordered by
--           Points descending (leaderboard order).
-- Read-only, safe to retry. Reads game.vw_HeroRankingCurrent for PeriodKind=0 (already joined to
-- game.Characters for the display name); PeriodKind=1 joins game.HeroRankings directly since the view only
-- covers the current period.
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
