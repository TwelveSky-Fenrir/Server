CREATE PROCEDURE admin.usp_GmAllowlist_Add @IpAddress VARCHAR(45)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM admin.GmAllowlists WHERE IpAddress = @IpAddress)
        THROW 50304, N'IP address is already on the GM allowlist.', 1;

    BEGIN TRY
        INSERT INTO admin.GmAllowlists (IpAddress)
        OUTPUT INSERTED.GmAllowlistId
        VALUES (@IpAddress);
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() IN (2627, 2601)
            THROW 50304, N'IP address is already on the GM allowlist.', 1;
        THROW;
    END CATCH
END;
