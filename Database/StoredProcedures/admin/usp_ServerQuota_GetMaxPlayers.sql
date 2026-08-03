CREATE PROCEDURE admin.usp_ServerQuota_GetMaxPlayers
AS
BEGIN
    SET NOCOUNT ON;

    SELECT MaxPlayers, GagePlayerNum
    FROM admin.ServerQuota
    WHERE Id = 1;
END;
