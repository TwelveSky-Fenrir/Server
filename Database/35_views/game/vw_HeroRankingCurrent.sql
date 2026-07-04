-- Current-period (PeriodKind=0) hero rankings joined with the character's display name.
CREATE VIEW game.vw_HeroRankingCurrent
AS
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
WHERE r.PeriodKind = 0;
