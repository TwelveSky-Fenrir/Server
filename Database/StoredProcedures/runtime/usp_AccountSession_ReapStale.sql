CREATE PROCEDURE runtime.usp_AccountSession_ReapStale
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @Reaped TABLE
                (
                    AccountId  INT     NOT NULL,
                    ServerKind TINYINT NOT NULL
                );

    DELETE
    FROM runtime.AccountSessions WITH (SNAPSHOT)
    OUTPUT deleted.AccountId,
           deleted.ServerKind
        INTO @Reaped (AccountId, ServerKind)
    WHERE LastRefreshedUtc <= DATEADD(MINUTE, -6, SYSUTCDATETIME());

    SELECT AccountId, ServerKind
    FROM @Reaped;
END;
