-- THROW 50305 if the (@MacAddress, @MachineGuid) pair already exists. NULL-safe comparison so a
-- repeat NULL-@MachineGuid row for the same @MacAddress also gets the documented error.
CREATE PROCEDURE admin.usp_MacRestriction_Add @MacAddress   VARCHAR(23),
    @MachineGuid  VARCHAR(128) = NULL,
    @AccountLimit INT = 1
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
EXISTS (
        SELECT 1
        FROM admin.MacRestrictions
        WHERE MacAddress = @MacAddress
          AND (MachineGuid = @MachineGuid OR (MachineGuid IS NULL AND @MachineGuid IS NULL))
    )
        THROW 50305, N'A restriction already exists for this MAC address / machine GUID pair.', 1;

INSERT INTO admin.MacRestrictions (MacAddress, MachineGuid, AccountLimit)
    OUTPUT INSERTED.MacRestrictionId
VALUES (@MacAddress, @MachineGuid, @AccountLimit);
END;
