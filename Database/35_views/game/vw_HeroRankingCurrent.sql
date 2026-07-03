-- Current-period (PeriodKind=0) hero rankings joined with the character's display name, meant to be read
-- from inside a retrieval procedure (security model: views are never granted/called directly by app code --
-- see 60_permissions/001_roles.sql). Every consumer of a leaderboard needs the character's name to render
-- it, so the join is done once here instead of by every caller.
-- INNER JOIN to game.Characters: a HeroRankings row can only ever exist for a real CharacterId (enforced by
-- FK_HeroRankings_Character), so there is no "orphan row" case a LEFT JOIN would need to preserve.
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
