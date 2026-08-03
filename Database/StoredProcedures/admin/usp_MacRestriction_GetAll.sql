CREATE PROCEDURE admin.usp_MacRestriction_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT MacRestrictionId, MacAddress, MachineGuid, AccountLimit
    FROM admin.MacRestrictions;
END;
