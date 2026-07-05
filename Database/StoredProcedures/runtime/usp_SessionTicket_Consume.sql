-- Not idempotent, never retryable: read-then-delete-then-select-if-valid. The DELETE always runs, so
-- a replay/duplicate call for the same AccountId finds nothing the second time (single-use ticket;
-- a blind retry here is the classic MMO ticket-dupe bug).
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
WHERE AccountId = @AccountId;

IF
@Exp IS NOT NULL AND @Exp > SYSUTCDATETIME()
SELECT @CharacterId AS CharacterId, @ShardId AS ShardId;
END;
