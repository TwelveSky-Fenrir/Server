CREATE PROCEDURE admin.usp_ShardMapAssignment_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ShardId, MapId
    FROM admin.ShardMapAssignments
    ORDER BY ShardId, MapId;
END;
