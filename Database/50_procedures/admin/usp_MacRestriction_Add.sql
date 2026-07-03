-- Contract: @MacAddress VARCHAR(23), @MachineGuid VARCHAR(128) NULL, @AccountLimit INT = 1
--           -> RS0 { MacRestrictionId INT }.
-- Errors: THROW 50305 if the (@MacAddress, @MachineGuid) pair already has a row (checked before insert;
-- UQ_MacRestrictions_MacAddress_MachineGuid is the last-resort backstop under a race). The explicit
-- NULL-safe comparison below gives a repeat NULL-@MachineGuid row for the same @MacAddress the same
-- documented error too, matching SQL Server's own NULL-in-unique-index semantics on that constraint.
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
