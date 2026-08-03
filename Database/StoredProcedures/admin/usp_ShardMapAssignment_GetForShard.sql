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
