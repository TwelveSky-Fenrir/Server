-- Proactively evicts a shard's row the instant a login-side zone-transfer reachability probe (a plain
-- TCP connect, see ZoneTransferService/TcpShardReachabilityProbe) finds it unreachable, instead of
-- leaving every other concurrent/subsequent usp_GameServer_GetDirectory read to wait out the full 15s
-- LastHeartbeatUtc freshness window (plus up to 2s of the caller's own in-memory cache reuse on top)
-- before the same dead shard finally ages out on its own. Idempotent: a shard already aged out, or
-- already evicted by a concurrent caller racing the same probe failure, is a silent no-op -- "already
-- gone" is the desired end state here, never an error.
CREATE PROCEDURE runtime.usp_GameServer_MarkUnreachable @ShardId TINYINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.GameServerDirectory
    WHERE ShardId = @ShardId;
END;
