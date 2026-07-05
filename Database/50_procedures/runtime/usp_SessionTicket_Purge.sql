-- Called on a server-side timer, not from the client path: runtime.SessionTickets is SCHEMA_ONLY
-- memory-optimized and has no background cleanup of its own -- expired rows sit there until deleted.
CREATE PROCEDURE runtime.usp_SessionTicket_Purge
WITH NATIVE_COMPILATION,
     SCHEMABINDING
         AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
DELETE
FROM runtime.SessionTickets
WHERE ExpiresAtUtc <= SYSUTCDATETIME();
END;
