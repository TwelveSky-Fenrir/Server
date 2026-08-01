-- Natively compiled: checked on every inbound connection attempt. Empty result set = not blocked
-- (native modules can't use EXISTS inside IF/WHILE, hence the plain SELECT instead of a BIT flag).
CREATE PROCEDURE admin.usp_BlockedIp_Exists @IpAddress VARCHAR(45)
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    SELECT BlockedIpId, CreatedAtUtc
    FROM admin.BlockedIps
    WHERE IpAddress = @IpAddress;
END;
