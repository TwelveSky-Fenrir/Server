-- Empty result means an unconfigured/unknown shard -- GameServer's boot-time check turns that into a
-- startup failure.
CREATE PROCEDURE admin.usp_ShardMapAssignment_GetForShard @ShardId TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT MapId
    FROM admin.ShardMapAssignments
    WHERE ShardId = @ShardId
    ORDER BY MapId;
END;
