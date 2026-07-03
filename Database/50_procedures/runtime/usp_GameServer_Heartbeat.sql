-- Contract: @ShardId TINYINT, @Host NVARCHAR(64), @Port INT, @Ccu INT, @Capacity INT,
--           @TickP99Ms REAL -> no result set.
-- Idempotent: each call fully overwrites the shard's row with its latest self-reported state,
-- including LastHeartbeatUtc = now. Upsert without MERGE (architecture reference §12.3): UPDATE
-- first, INSERT only when nothing matched (@@ROWCOUNT = 0) -- a shard restarting under the same
-- @ShardId simply reappears with a fresh row.
-- Called every 5 s by each GameServer (architecture reference §9.2). Freshness of this row is what
-- usp_GameServer_GetDirectory filters on -- stop heartbeating and the shard silently drops out of
-- the LoginServer's offer, which doubles as the maintenance-drain mechanism.
CREATE PROCEDURE runtime.usp_GameServer_Heartbeat @ShardId   TINYINT,
    @Host      NVARCHAR(64),
    @Port      INT,
    @Ccu       INT,
    @Capacity  INT,
    @TickP99Ms REAL
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
UPDATE runtime.GameServerDirectory
SET Host             = @Host,
    Port             = @Port,
    Ccu              = @Ccu,
    Capacity         = @Capacity,
    TickP99Ms        = @TickP99Ms,
    LastHeartbeatUtc = SYSUTCDATETIME()
WHERE ShardId = @ShardId;

IF
@@ROWCOUNT = 0
        INSERT INTO runtime.GameServerDirectory
            (ShardId, Host, Port, Ccu, Capacity, TickP99Ms, LastHeartbeatUtc)
        VALUES
            (@ShardId, @Host, @Port, @Ccu, @Capacity, @TickP99Ms, SYSUTCDATETIME());
END;
