-- Cross-shard guild-points leaderboard. game.Guilds.Points is a genuinely hot, cluster-wide-shared
-- counter: usp_Guild_AddFourGuildPoints credits it on every RvR kill in a zone-049-type event (from
-- whichever shard hosts that map) and usp_Guild_AdjustPoints covers every other point-granting flow --
-- unlike GameServer's boot-time in-memory caches (world reference data, RvR/tower/guild-ranking snapshots
-- loaded once before host.RunAsync()), a per-shard cache of this data would go stale the instant a
-- DIFFERENT shard's kill processing increments any guild's Points, so this stays a live, per-query SQL
-- read rather than something one process can safely own for its own lifetime.
--
-- Deliberately a PLAIN view, and disqualified from ever becoming an indexed one on three INDEPENDENT
-- grounds (verified against Microsoft Learn's "Create indexed views"):
--   1. Write cost: Points changes on every RvR kill -- the same losing write-vs-read trade-off already
--      documented on game.vw_GuildRosterCounts/game.vw_HeroRankingCurrent's own header comments.
--   2. RANK() OVER is a ranking/window function -- explicitly listed among the constructs an indexed
--      view's SELECT list must not contain.
--   3. This view references game.vw_GuildRosterCounts, itself a VIEW -- indexed views may reference only
--      base tables via two-part names and can never reference another view, independent of everything
--      else about this query.
--
-- RANK() (not ROW_NUMBER()): two guilds with equal Points share the same rank, matching how a leaderboard
-- UI normally displays a draw. The OVER clause deliberately orders by g.Points DESC ALONE, not also by
-- GuildId -- RANK() only ties rows whose ENTIRE ORDER BY key is equal, so adding GuildId (unique per row)
-- as a second key would make every row distinct and silently degrade RANK() into ROW_NUMBER(), defeating
-- the whole point of choosing RANK() here. GuildId ASC is instead the consuming procedure's own OUTER
-- ORDER BY tiebreak, for stable *output row order* among tied ranks -- it must never leak into the OVER
-- clause itself. RANK() itself returns BIGINT (Microsoft Learn, "RANK (Transact-SQL)") but a rank can never
-- exceed the guild count, so it is cast down to INT here rather than forcing every consumer to carry a
-- BIGINT for a value this small.
CREATE VIEW game.vw_GuildRanking
AS
SELECT g.GuildId,
       g.Name,
       g.Points,
       COALESCE(c.MemberCount, 0)                        AS MemberCount,
       CAST(RANK() OVER (ORDER BY g.Points DESC) AS INT) AS RankNo
FROM game.Guilds g
         LEFT JOIN game.vw_GuildRosterCounts c ON c.GuildId = g.GuildId;
