CREATE PROCEDURE admin.usp_FirewallRule_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT FirewallRuleId, IpAddress, RuleType
    FROM admin.FirewallRules;
END;
