-- No-op if the row is already gone (account never claimed a session, or a prior teardown already cleared
-- it) -- called best-effort from both connection hosts' finally blocks, wrapped in try/catch-log there so a
-- failure here must never throw out of a finally block.
CREATE PROCEDURE runtime.usp_AccountSession_MarkTearingDown @AccountId INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    UPDATE runtime.AccountSessions
    SET SessionState = 1
    WHERE AccountId = @AccountId;
END;
