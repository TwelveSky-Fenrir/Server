CREATE PROCEDURE runtime.usp_AccountSession_GetActiveCount
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT COUNT(*)
    FROM runtime.AccountSessions WITH (SNAPSHOT);
END;
