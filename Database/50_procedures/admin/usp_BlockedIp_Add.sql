-- Contract: @IpAddress VARCHAR(45) -> RS0 { BlockedIpId INT }.
-- Interpreted (not native): memory-optimized tables are readable/writable from ordinary T-SQL too (only
-- *natively compiled procedures* are restricted to memory-optimized-only access) -- adding a blocked IP
-- is an infrequent moderation action, not the hot path, so it doesn't need native compilation the way
-- admin.usp_BlockedIp_Exists does.
-- Errors: THROW 50302 if @IpAddress is already blocked (checked before insert; UQ_BlockedIps_IpAddress
-- is the last-resort backstop under a race).
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
