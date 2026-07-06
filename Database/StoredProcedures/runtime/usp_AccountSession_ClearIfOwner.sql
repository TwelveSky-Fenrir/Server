-- Idempotent: matches on ServerKind/ShardId/SessionToken together, so a session that already handed off to
-- a different ServerKind/ShardId (row moved on -- e.g. a Login row that already transitioned to Game) or was
-- superseded by a newer SessionToken correctly no-ops instead of deleting someone else's live row. Used both
-- by normal disconnect and by post-kick cleanup.
CREATE PROCEDURE runtime.usp_AccountSession_ClearIfOwner @AccountId    INT,
    @ServerKind   TINYINT,
    @ShardId      TINYINT NULL,
    @SessionToken UNIQUEIDENTIFIER
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
DELETE
FROM runtime.AccountSessions
WHERE AccountId = @AccountId
  AND ServerKind = @ServerKind
  AND (@ShardId IS NULL OR ShardId = @ShardId)
  AND SessionToken = @SessionToken;
END;
