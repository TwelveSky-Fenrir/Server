CREATE PROCEDURE admin.usp_BlockedIp_Remove @IpAddress VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DELETE
    FROM admin.BlockedIps
    WHERE IpAddress = @IpAddress;

    SELECT RemovedCount = @@ROWCOUNT;
END;
