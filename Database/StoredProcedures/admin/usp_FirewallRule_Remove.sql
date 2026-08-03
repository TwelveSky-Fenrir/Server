CREATE PROCEDURE admin.usp_FirewallRule_Remove @IpAddress VARCHAR(45)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DELETE
    FROM admin.FirewallRules
    WHERE IpAddress = @IpAddress;

    SELECT RemovedCount = @@ROWCOUNT;
END;
