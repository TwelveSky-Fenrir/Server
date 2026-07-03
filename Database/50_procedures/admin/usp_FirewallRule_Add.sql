-- Contract: @IpAddress VARCHAR(45), @RuleType TINYINT -> RS0 { FirewallRuleId INT }.
-- Errors: THROW 50303 if @IpAddress already has a rule (checked before insert;
-- UQ_FirewallRules_IpAddress is the last-resort backstop under a race).
CREATE PROCEDURE admin.usp_FirewallRule_Add @IpAddress VARCHAR(45),
    @RuleType  TINYINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
EXISTS (SELECT 1 FROM admin.FirewallRules WHERE IpAddress = @IpAddress)
        THROW 50303, N'A firewall rule already exists for this IP address.', 1;

INSERT INTO admin.FirewallRules (IpAddress, RuleType)
    OUTPUT INSERTED.FirewallRuleId
VALUES (@IpAddress, @RuleType);
END;
