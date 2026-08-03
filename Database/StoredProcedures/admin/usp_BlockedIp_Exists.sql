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
