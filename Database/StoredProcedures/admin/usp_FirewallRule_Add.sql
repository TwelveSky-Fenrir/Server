CREATE PROCEDURE admin.usp_FirewallRule_Add @IpAddress VARCHAR(45),
                                            @RuleType TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM admin.FirewallRules WHERE IpAddress = @IpAddress)
        THROW 50303, N'A firewall rule already exists for this IP address.', 1;

    BEGIN TRY
        INSERT INTO admin.FirewallRules (IpAddress, RuleType)
        OUTPUT INSERTED.FirewallRuleId
        VALUES (@IpAddress, @RuleType);
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() IN (2627, 2601)
            THROW 50303, N'A firewall rule already exists for this IP address.', 1;
        THROW;
    END CATCH
END;
