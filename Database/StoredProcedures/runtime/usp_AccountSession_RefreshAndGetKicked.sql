-- Batched per-shard poll: doubles as the liveness refresh the 6-minute staleness sweep
-- (usp_AccountSession_ReapStale) depends on, and is the kick-delivery mechanism for a Game-side session
-- (@ServerKind = 1). Called with @ServerKind = 0 / @ShardId = NULL from the Login-side liveness poll too,
-- whose result is always empty by construction -- only Login-time claim (usp_AccountSession_ClaimOrSignalKick)
-- clears/refuses Login rows synchronously, so a Login row is never KickRequested.
--
-- Interpreted, not native-compiled, unlike its siblings in this feature: natively compiled modules don't
-- support UPDATE ... FROM or subqueries (needed here to bulk-touch every row named by the incoming TVP), so
-- this one stays interop Transact-SQL against the memory-optimized runtime.AccountSessions table. The
-- WITH (SNAPSHOT) table hints make the required isolation level explicit regardless of the caller's ambient
-- transaction/autocommit state (see "Transactions with Memory-Optimized Tables").
CREATE PROCEDURE runtime.usp_AccountSession_RefreshAndGetKicked @ServerKind TINYINT,
    @ShardId    TINYINT NULL,
    @AccountIds runtime.tvp_AccountIdList READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE s
SET s.LastRefreshedUtc = SYSUTCDATETIME() FROM runtime.AccountSessions AS s
WITH (SNAPSHOT)
    INNER JOIN @AccountIds AS a
ON a.AccountId = s.AccountId
WHERE s.ServerKind = @ServerKind
  AND (@ShardId IS NULL
   OR s.ShardId = @ShardId);

SELECT s.AccountId, s.SessionToken
FROM runtime.AccountSessions AS s WITH (SNAPSHOT)
             INNER JOIN @AccountIds AS a
ON a.AccountId = s.AccountId
WHERE s.KickRequested = 1
  AND s.ServerKind = @ServerKind
  AND (@ShardId IS NULL
   OR s.ShardId = @ShardId);
END;
