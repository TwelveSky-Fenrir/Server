CREATE PROCEDURE admin.usp_FirewallRule_Upsert @IpAddress VARCHAR(45),
                                               @RuleType TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE admin.FirewallRules WITH (UPDLOCK, HOLDLOCK)
    SET RuleType = @RuleType
    WHERE IpAddress = @IpAddress;

    IF
        @@ROWCOUNT = 0
        INSERT INTO admin.FirewallRules (IpAddress, RuleType)
        VALUES (@IpAddress, @RuleType);

    COMMIT TRANSACTION;
END;
