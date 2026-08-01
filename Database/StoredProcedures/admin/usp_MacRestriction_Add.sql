-- THROW 50305 if the (@MacAddress, @MachineGuid) pair already exists. NULL-safe comparison so a
-- repeat NULL-@MachineGuid row for the same @MacAddress also gets the documented error. The pre-check
-- below is only the fast path for the ordinary (non-racing) case -- under RCSI the pre-check's own read
-- never blocks a concurrent writer, so two concurrent Add calls for the same brand-new pair can both pass
-- it before either commits (UQ_MacRestrictions_MacAddress_MachineGuid genuinely backstops this even for
-- the NULL-MachineGuid case: SQL Server unique constraints treat two NULLs as equal for uniqueness
-- purposes, unlike ANSI SQL's usual NULL <> NULL -- Microsoft Learn, "Create unique constraints"). The
-- TRY/CATCH around the INSERT is what actually guarantees the caller always observes the catalogued 50305
-- rather than a raw constraint-violation error on the race's loser.
CREATE PROCEDURE admin.usp_MacRestriction_Add @MacAddress VARCHAR(23),
                                              @MachineGuid VARCHAR(128) = NULL,
                                              @AccountLimit INT = 1
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1
                FROM admin.MacRestrictions
                WHERE MacAddress = @MacAddress
                  AND (MachineGuid = @MachineGuid OR (MachineGuid IS NULL AND @MachineGuid IS NULL)))
        THROW 50305, N'A restriction already exists for this MAC address / machine GUID pair.', 1;

    BEGIN TRY
        INSERT INTO admin.MacRestrictions (MacAddress, MachineGuid, AccountLimit)
        OUTPUT INSERTED.MacRestrictionId
        VALUES (@MacAddress, @MachineGuid, @AccountLimit);
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() IN (2627, 2601)
            THROW 50305, N'A restriction already exists for this MAC address / machine GUID pair.', 1;
        THROW;
    END CATCH
END;
