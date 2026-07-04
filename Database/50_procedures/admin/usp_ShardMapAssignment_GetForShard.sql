-- database/50_procedures/admin/usp_ShardMapAssignment_GetForShard.sql
-- Contract: @ShardId -> RS0 { MapId }, one row per map this shard hosts, MapId ascending. Empty result
-- means an unconfigured/unknown shard -- GameServer's own boot-time check turns that into a startup
-- failure (see GameServerOptions.ShardId's own remarks), the same posture as GameServerOptionsValidator
-- previously enforced on the (now removed) Game:Maps config list.
-- Read-only, safe to retry.
CREATE PROCEDURE admin.usp_ShardMapAssignment_GetForShard @ShardId TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT MapId
    FROM admin.ShardMapAssignments
    WHERE ShardId = @ShardId
    ORDER BY MapId;
END;
