CREATE PROCEDURE admin.usp_FirewallRule_ReconcileAllowlist
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DELETE
    FROM admin.FirewallRules
    WHERE RuleType IN (0, 2, 4, 5)
       OR (ExpiresAtUtc IS NOT NULL AND ExpiresAtUtc <= SYSUTCDATETIME());
END;
