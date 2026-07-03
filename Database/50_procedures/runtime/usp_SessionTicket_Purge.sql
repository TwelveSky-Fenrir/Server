-- Contract: no parameters -> no result set. Deletes every ticket whose ExpiresAtUtc has already
-- passed. Idempotent (a run with nothing expired is a no-op).
-- Called on a server-side timer, not from the client path: runtime.SessionTickets is SCHEMA_ONLY
-- memory-optimized (§12.4) and has no background cleanup of its own -- expired rows just sit there,
-- occupying a hash bucket, until something deletes them.
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
