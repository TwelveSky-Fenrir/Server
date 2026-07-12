-- Cross-checks two independently-written cluster-wide MEMORY_OPTIMIZED tables for a future admin/ops
-- dashboard: runtime.AccountSessions is the session-of-record (Login/Game claim + heartbeat-refresh
-- state, authoritative for "is this account actually online"), while runtime.GameServerDirectory.Ccu is
-- each shard's own self-reported connection count from its most recent 5s heartbeat
-- (runtime.usp_GameServer_Heartbeat). The two numbers should track each other closely; persistent drift
-- (e.g. TotalSessions >> Ccu, or a shard with sessions but a stale LastHeartbeatUtc) is the signature of a
-- shard that crashed/stopped heartbeating while still holding stale runtime.AccountSessions rows for
-- runtime.usp_AccountSession_ReapStale to eventually clean up.
--
-- Every shard that has ever heartbeat at least once keeps its runtime.GameServerDirectory row forever (no
-- physical delete -- see that table's own header comment), so driving this view from a LEFT JOIN off the
-- directory means a shard currently at zero sessions still reports a row with TotalSessions = 0 instead of
-- silently disappearing.
--
-- ShardId is only meaningful on runtime.AccountSessions when ServerKind = 1 (Game) -- see that table's own
-- header -- so the aggregation below filters to ServerKind = 1 before grouping.
--
-- Deliberately a plain (not indexed) view, and not merely for the usual write-frequency reason: both base
-- tables are WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY), and per Microsoft Learn ("Accessing
-- Memory-Optimized Tables Using Interpreted Transact-SQL", Area "Access to tables": "Referencing a
-- memory-optimized table from an indexed view" is listed as unsupported) an indexed view can never
-- reference a memory-optimized base table at all, full stop -- this repo's entire runtime.* schema is
-- structurally ineligible for view-indexing, independent of how hot its writes are.
--
-- WITH (SNAPSHOT) on both memory-optimized reads: not required by the cross-container disk/MO-mixing rule
-- (SQL Server error 41359) since nothing disk-based is touched in this view at all -- but memory-optimized
-- tables under READ_COMMITTED_SNAPSHOT still only support READ COMMITTED in autocommit mode; an explicit
-- or implicit transaction reading them requires an explicit isolation hint (error 41368). Hinting
-- explicitly here matches runtime.usp_AccountSession_GetActiveCount's own established convention, so this
-- view keeps working regardless of the caller's ambient transaction state.
CREATE VIEW admin.vw_ActiveSessionsByShard
AS
SELECT d.ShardId,
       d.Host,
       d.Port,
       d.Ccu,
       d.Capacity,
       d.TickP99Ms,
       d.LastHeartbeatUtc,
       ISNULL(s.TotalSessions, 0)       AS TotalSessions,
       ISNULL(s.ActiveSessions, 0)      AS ActiveSessions,
       ISNULL(s.TearingDownSessions, 0) AS TearingDownSessions,
       s.MostRecentSessionRefreshUtc
FROM runtime.GameServerDirectory AS d WITH (SNAPSHOT)
    LEFT JOIN (
        SELECT ShardId,
               COUNT(*)                                          AS TotalSessions,
               SUM(CASE WHEN SessionState = 0 THEN 1 ELSE 0 END) AS ActiveSessions,
               SUM(CASE WHEN SessionState = 1 THEN 1 ELSE 0 END) AS TearingDownSessions,
               MAX(LastRefreshedUtc)                             AS MostRecentSessionRefreshUtc
        FROM runtime.AccountSessions WITH (SNAPSHOT)
        WHERE ServerKind = 1
        GROUP BY ShardId
    ) AS s ON s.ShardId = d.ShardId;
