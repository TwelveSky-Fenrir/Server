-- THROW 50303 if @IpAddress already has a rule. The pre-check below is only the fast path for the
-- ordinary (non-racing) case -- under RCSI the pre-check's own read never blocks a concurrent writer, so
-- two concurrent Add calls for the same brand-new IP can both pass it before either commits. The TRY/CATCH
-- around the INSERT is what actually guarantees the caller always observes the catalogued 50303 rather
-- than a raw UQ_FirewallRules_IpAddress constraint-violation error on the race's loser.
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
