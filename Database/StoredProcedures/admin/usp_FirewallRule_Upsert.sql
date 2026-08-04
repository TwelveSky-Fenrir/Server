CREATE PROCEDURE admin.usp_FirewallRule_Upsert @IpAddress VARCHAR(45),
                                               @RuleType TINYINT,
                                               @TtlSeconds INT = NULL
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE @ExpiresAtUtc DATETIME2(3) = CASE
                                             WHEN @TtlSeconds IS NULL THEN NULL
                                             ELSE DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME())
        END;

    BEGIN TRANSACTION;

    UPDATE admin.FirewallRules WITH (UPDLOCK, HOLDLOCK)
    SET RuleType     = @RuleType,
        ExpiresAtUtc = CASE
                           WHEN RuleType IN (1, 3) AND ExpiresAtUtc IS NULL THEN NULL
                           WHEN RuleType IN (1, 3) AND ExpiresAtUtc >= @ExpiresAtUtc THEN ExpiresAtUtc
                           ELSE @ExpiresAtUtc
            END
    WHERE IpAddress = @IpAddress;

    IF
        @@ROWCOUNT = 0
        INSERT INTO admin.FirewallRules (IpAddress, RuleType, ExpiresAtUtc)
        VALUES (@IpAddress, @RuleType, @ExpiresAtUtc);

    COMMIT TRANSACTION;
END;
