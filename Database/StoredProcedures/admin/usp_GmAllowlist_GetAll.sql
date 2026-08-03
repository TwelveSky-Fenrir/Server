CREATE PROCEDURE admin.usp_GmAllowlist_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT GmAllowlistId, IpAddress, CreatedAtUtc
    FROM admin.GmAllowlists;
END;
