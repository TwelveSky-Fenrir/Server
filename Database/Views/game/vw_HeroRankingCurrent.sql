-- Current-period (PeriodKind=0) hero rankings joined with the character's display name.
-- Not an indexed-view candidate: game.HeroRankings.Points is written per PvP kill via
-- game.usp_HeroRanking_AddPoints (flushed by HeroRankPointsWriteBehindHost), so indexing this view would
-- charge every kill-reward grant, on every shard, for index maintenance an occasional leaderboard read
-- doesn't need. IX_HeroRankings_Period_Points NONCLUSTERED (PeriodKind, Points DESC) already covers the
-- read without materializing anything.
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
