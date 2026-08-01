-- Interpreted, not native: an infrequent moderation action, unlike the hot-path usp_BlockedIp_Exists.
-- THROW 50302 if @IpAddress is already blocked. The pre-check below is only the fast path for the ordinary
-- (non-racing) case -- under RCSI the pre-check's own read never blocks a concurrent writer, so two
-- concurrent Add calls for the same brand-new IP can both pass it before either commits. The TRY/CATCH
-- around the INSERT is what actually guarantees the caller always observes the catalogued 50302 rather
-- than a raw UQ_BlockedIps_IpAddress constraint-violation error on the race's loser.
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
