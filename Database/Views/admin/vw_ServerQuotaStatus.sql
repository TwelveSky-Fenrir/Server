-- Cluster-wide login-capacity snapshot for a future admin/ops dashboard: admin.ServerQuota's MaxPlayers/
-- GagePlayerNum are an operator-tuned singleton that only ever changes when a GM deliberately retunes it
-- (see that table's own header), crossed with a live COUNT(*) over runtime.AccountSessions -- the same
-- cluster-wide tally runtime.usp_AccountSession_GetActiveCount already computes for the login-time
-- server-full gate (Fenrir.Application.Login.Hosting.ServerQuotaRefreshHost polls both independently into
-- LoginCapacityState on its own ~1s cadence; this view exists for a future ops tool that wants the same
-- two numbers in a single read, not to replace that hot C#-side path).
--
-- Real aggregation (COUNT(*)) over a table that changes on every login/logout/kick cluster-wide, crossed
-- with operator-tuned config that essentially never changes at runtime -- not a "free" view wrapping a
-- single table with nothing computed.
--
-- Deliberately a plain (not indexed) view. Two independent disqualifiers apply: (1) runtime.AccountSessions
-- is WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY), and per Microsoft Learn ("Accessing
-- Memory-Optimized Tables Using Interpreted Transact-SQL") an indexed view can never reference a
-- memory-optimized base table at all, full stop; (2) even ignoring that, runtime.AccountSessions mutates
-- on every connect/disconnect/kick cluster-wide -- indexing would pay that write cost for a read an admin
-- dashboard needs at most a few times a minute.
--
-- WITH (SNAPSHOT) on the memory-optimized read is REQUIRED here, not just a defensive convention: this
-- statement mixes a memory-optimized table (runtime.AccountSessions) with a disk-based one
-- (admin.ServerQuota) while READ_COMMITTED_SNAPSHOT = ON (Migrations/000_init/001_database_options.sql),
-- which SQL Server flatly rejects under READ COMMITTED without an explicit isolation hint on the
-- memory-optimized side (error 41359, "A query that accesses memory optimized tables using the READ
-- COMMITTED isolation level, cannot access disk based tables when the database option
-- READ_COMMITTED_SNAPSHOT is set to ON"). Same cross-container pattern game.usp_TribeBank_Withdraw already
-- established for mixing game.TribeBank with disk-based tables/table variables.
CREATE VIEW admin.vw_ServerQuotaStatus
AS
SELECT q.MaxPlayers,
       q.GagePlayerNum,
       s.CurrentPlayers
FROM admin.ServerQuota AS q
         CROSS JOIN (SELECT COUNT(*) AS CurrentPlayers FROM runtime.AccountSessions WITH (SNAPSHOT)) AS s;
