-- THROW 50304 if @IpAddress is already allowlisted. The pre-check below is only the fast path for the
-- ordinary (non-racing) case -- under RCSI the pre-check's own read never blocks a concurrent writer, so
-- two concurrent Add calls for the same brand-new IP can both pass it before either commits. The TRY/CATCH
-- around the INSERT is what actually guarantees the caller always observes the catalogued 50304 rather
-- than a raw UQ_GmAllowlists_IpAddress constraint-violation error on the race's loser.
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
