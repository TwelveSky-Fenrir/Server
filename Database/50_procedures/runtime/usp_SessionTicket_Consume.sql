-- Contract: @AccountId INT -> RS0 { CharacterId INT, ShardId TINYINT } | empty result set if no
-- ticket exists for this account, or if it already expired.
-- Not idempotent, never retryable (architecture reference §11.6): read into local variables, THEN
-- delete, THEN select the captured variables only if they were non-null and unexpired -- the DELETE
-- always runs, so a replay/duplicate call for the same AccountId finds nothing the second time
-- (single-use, ADR-0005). A blind retry on this path is the classic MMO ticket-dupe bug.
CREATE PROCEDURE runtime.usp_SessionTicket_Consume @AccountId INT
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE
@CharacterId INT, @ShardId TINYINT, @Exp DATETIME2(3);

SELECT @CharacterId = CharacterId, @ShardId = ShardId, @Exp = ExpiresAtUtc
FROM runtime.SessionTickets
WHERE AccountId = @AccountId;

DELETE
FROM runtime.SessionTickets
WHERE AccountId = @AccountId; -- read+destroy: atomic, single-use

IF
@Exp IS NOT NULL AND @Exp > SYSUTCDATETIME()
SELECT @CharacterId AS CharacterId, @ShardId AS ShardId;
END;
