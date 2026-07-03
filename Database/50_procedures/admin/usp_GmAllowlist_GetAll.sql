-- Contract: no parameters -> RS0, every row. Loaded once at boot (see admin.GmAllowlist's table
-- comment) -- GM console connections are rare compared to the player login path, so this doesn't need
-- the per-connection native-compiled treatment admin.usp_BlockedIp_Exists gets.
CREATE PROCEDURE admin.usp_GmAllowlist_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT GmAllowlistId, IpAddress, CreatedAtUtc
FROM admin.GmAllowlist;
END;
