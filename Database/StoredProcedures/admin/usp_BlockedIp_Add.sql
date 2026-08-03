CREATE PROCEDURE admin.usp_BlockedIp_Add @IpAddress VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM admin.BlockedIps WHERE IpAddress = @IpAddress)
        THROW 50302, N'IP address is already blocked.', 1;

    BEGIN TRY
        INSERT INTO admin.BlockedIps (IpAddress)
        OUTPUT INSERTED.BlockedIpId
        VALUES (@IpAddress);
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() IN (2627, 2601)
            THROW 50302, N'IP address is already blocked.', 1;
        THROW;
    END CATCH
END;
