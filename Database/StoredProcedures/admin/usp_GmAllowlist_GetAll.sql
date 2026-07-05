-- Loaded once at boot; GM console connections are rare, so no native-compiled treatment needed here.
CREATE PROCEDURE admin.usp_GmAllowlist_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT GmAllowlistId, IpAddress, CreatedAtUtc
FROM admin.GmAllowlist;
END;
