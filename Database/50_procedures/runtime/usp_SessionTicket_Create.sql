-- Contract: @AccountId INT, @CharacterId INT, @ShardId TINYINT, @TtlSeconds INT -> no result set.
-- Idempotent: a single active ticket exists per AccountId at any time (ADR-0005) -- a second Login
-- for the same account before the previous ticket is consumed simply supersedes it. DELETE-then-
-- INSERT, never MERGE (architecture reference §12.3); the DELETE is a no-op when nothing is there.
-- Natively compiled against runtime.SessionTickets (memory-optimized, SCHEMA_ONLY, §12.4): this is
-- the hottest write on the LoginServer handover path (§8.2), so it runs with zero locks/latches and
-- zero T-SQL interpretation.
CREATE PROCEDURE runtime.usp_SessionTicket_Create @AccountId   INT,
    @CharacterId INT,
    @ShardId     TINYINT,
    @TtlSeconds  INT
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
DELETE
FROM runtime.SessionTickets
WHERE AccountId = @AccountId;

INSERT INTO runtime.SessionTickets (AccountId, CharacterId, ShardId, ExpiresAtUtc)
VALUES (@AccountId, @CharacterId, @ShardId, DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME()));
END;
