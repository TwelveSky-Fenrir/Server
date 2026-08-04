CREATE PROCEDURE admin.usp_FirewallRule_Add @IpAddress VARCHAR(45),
                                            @RuleType TINYINT,
                                            @TtlSeconds INT = NULL
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        @RuleType NOT IN (1, 3)
        THROW 50307, N'A firewall rule must use a blocking rule type (1 = TCP, 3 = any).', 1;

    DECLARE @ExpiresAtUtc DATETIME2(3) = CASE
                                             WHEN @TtlSeconds IS NULL THEN NULL
                                             ELSE DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME())
        END;

    BEGIN TRANSACTION;

    DELETE
    FROM admin.FirewallRules WITH (UPDLOCK, HOLDLOCK)
    WHERE IpAddress = @IpAddress
      AND ExpiresAtUtc <= SYSUTCDATETIME();

    IF
        EXISTS (SELECT 1
                FROM admin.FirewallRules
                WITH (UPDLOCK, HOLDLOCK)
                WHERE IpAddress = @IpAddress)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50303, N'A firewall rule already exists for this IP address.', 1;
        END;

    BEGIN TRY
        INSERT INTO admin.FirewallRules (IpAddress, RuleType, ExpiresAtUtc)
        OUTPUT INSERTED.FirewallRuleId
        VALUES (@IpAddress, @RuleType, @ExpiresAtUtc);
    END TRY
    BEGIN CATCH
        IF
            @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        IF ERROR_NUMBER() IN (2627, 2601)
            THROW 50303, N'A firewall rule already exists for this IP address.', 1;

        THROW;
    END CATCH;

    COMMIT TRANSACTION;
END;
