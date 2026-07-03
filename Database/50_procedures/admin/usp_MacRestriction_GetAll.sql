-- Contract: no parameters -> RS0, every row. Loaded once at LoginServer boot (see
-- admin.MacRestrictions's table comment) -- checked at account creation / MAC registration time, not
-- on every packet.
CREATE PROCEDURE admin.usp_MacRestriction_GetAll
    AS
BEGIN
    SET
NOCOUNT ON;

SELECT MacRestrictionId, MacAddress, MachineGuid, AccountLimit
FROM admin.MacRestrictions;
END;
