-- Interpreted, not native: an infrequent moderation action, unlike the hot-path usp_BlockedIp_Exists.
-- THROW 50302 if @IpAddress is already blocked (checked before insert; UQ_BlockedIps_IpAddress is the
-- last-resort backstop under a race).
CREATE PROCEDURE admin.usp_BlockedIp_Add @IpAddress VARCHAR(45)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM admin.BlockedIps WHERE IpAddress = @IpAddress)
        THROW 50302, N'IP address is already blocked.', 1;

    INSERT INTO admin.BlockedIps (IpAddress)
    OUTPUT INSERTED.BlockedIpId
    VALUES (@IpAddress);
END;
