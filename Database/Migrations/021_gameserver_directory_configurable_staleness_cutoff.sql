-- Corrective follow-up for runtime.usp_GameServer_GetDirectory (StoredProcedures/runtime/
-- usp_GameServer_GetDirectory.sql, never edited in place -- DbMigrator journals every script by content hash
-- and refuses a changed re-apply).
--
-- Gap (behavior contract: gameserver-directory-heartbeat-liveness, severity Major): the procedure's staleness
-- filter (`WHERE LastHeartbeatUtc > DATEADD(SECOND, -15, SYSUTCDATETIME())`) was a bare literal, not derived
-- from or validated against Game:HeartbeatIntervalSeconds (Application/Fenrir.Application.Game.Domain/
-- GameServerOptions.cs, default 5s -- three heartbeats fit comfortably inside the shipped 15s window). Nothing
-- anywhere enforced that an operator raising HeartbeatIntervalSeconds toward/past 15s would keep a healthy
-- shard's directory row inside this filter: the only symptom would have been that shard silently flapping in
-- and out of (or vanishing entirely from) ZoneTransferService's routing pool, with no boot-time or runtime
-- signal pointing at the cause -- the mismatch was only discoverable by reading two files in two different
-- projects/layers together.
--
-- Fix (SQL half of this finding -- the two files above live outside Database/Infrastructure.Fenrir.Data's own
-- scope): replace the bare `-15` literal with an explicit `@StalenessCutoffSeconds` parameter, still defaulting
-- to 15 so every existing caller (LoginServer's cached directory read, GameServer's own boot-time
-- ShardPartitionGuard/SingletonRvrSchedulerGuard reads) keeps working unmodified with the same default cutoff
-- as before. The default now lives in exactly one place C#-side too --
-- Fenrir.Data.Abstractions.Runtime.GameServerDirectoryDefaults.StalenessCutoffSeconds -- instead of being
-- duplicated as an unreferenced comment; GameServerDirectoryRepository.GetDirectoryAsync gained an overload
-- taking an explicit cutoff for a caller that knows its own configured heartbeat cadence. Closing the other
-- half of this finding (a boot-time check that Game:HeartbeatIntervalSeconds stays comfortably under this same
-- constant) belongs in Application/Fenrir.Application.Game.Domain/GameServerOptionsValidator.cs, which this
-- script does not touch.
ALTER PROCEDURE runtime.usp_GameServer_GetDirectory @StalenessCutoffSeconds INT = 15
    WITH NATIVE_COMPILATION ,
        SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    SELECT ShardId, Host, Port, Ccu, Capacity, TickP99Ms
    FROM runtime.GameServerDirectory
    WHERE LastHeartbeatUtc > DATEADD(SECOND, -@StalenessCutoffSeconds, SYSUTCDATETIME());
END;
GO
