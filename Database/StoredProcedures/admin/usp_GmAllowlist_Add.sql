-- THROW 50304 if @IpAddress is already allowlisted (checked before insert; UQ_GmAllowlist_IpAddress
-- is the last-resort backstop under a race).
CREATE PROCEDURE admin.usp_GmAllowlist_Add @IpAddress VARCHAR(45)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM admin.GmAllowlist WHERE IpAddress = @IpAddress)
        THROW 50304, N'IP address is already on the GM allowlist.', 1;

    INSERT INTO admin.GmAllowlist (IpAddress)
    OUTPUT INSERTED.GmAllowlistId
    VALUES (@IpAddress);
END;
