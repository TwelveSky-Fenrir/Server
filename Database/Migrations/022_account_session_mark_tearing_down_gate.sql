-- Corrective fix for runtime.usp_AccountSession_MarkTearingDown
-- (StoredProcedures/runtime/usp_AccountSession_MarkTearingDown.sql, never edited in place -- DbMigrator
-- journals every script by content hash and refuses a changed re-apply).
--
-- Bug (behavior contract: account-session-record-must-not-be-affected-by-a-non-owning-teardown, severity
-- Blocker): the shipped procedure unconditionally sets SessionState = 1 for @AccountId alone, with no
-- ServerKind/ShardId/SessionToken match at all -- unlike its sibling usp_AccountSession_ClearIfOwner
-- (StoredProcedures/runtime/usp_AccountSession_ClearIfOwner.sql), the very next step in the same teardown
-- sequence, which is already correctly gated on all three. Both LoginConnectionHost.TearDownAccountSessionAsync
-- (Application/Fenrir.Application.Login.Hosting/LoginConnectionHost.cs) and GameConnectionHost's identical copy
-- (Application/Fenrir.Application.Game.Hosting/GameConnectionHost.cs) call this procedure unconditionally from
-- the connection's `finally` block for every connection that ever recorded an AccountId -- i.e. on essentially
-- every ordinary login-to-zone handoff, since the client is expected to disconnect from Login shortly after
-- receiving its zone-transfer ticket while a separate Game-side connection is independently completing its own
-- usp_AccountSession_TransitionToGame claim for the same account. The two events race with no ordering
-- guarantee between them:
--   * Login-teardown-first: this unconditional UPDATE reaches the row before the Game-side claim, so
--     usp_AccountSession_TransitionToGame's own WHERE clause (which requires SessionState = 0) no longer
--     matches, @@ROWCOUNT = 0, Accepted = 0, and ZoneHandshakeService.ConsumeTicketAsync reports
--     ZoneHandshakeOutcome.SessionSuperseded for a perfectly ordinary player who never had a second login.
--   * Game-claim-first: the claim succeeds, but this unconditional UPDATE still runs afterward regardless and
--     sets SessionState = 1 on the now-Game-owned row anyway. Nothing else that touches this table ever
--     transitions SessionState back to 0 short of the row being deleted outright
--     (usp_AccountSession_ReapStale only ever deletes; usp_AccountSession_GetActiveCount and
--     usp_AccountSession_RefreshAndGetKicked never reference SessionState at all), so for the rest of that
--     legitimate session, any genuine second-login attempt for the same account hits
--     usp_AccountSession_ClaimOrSignalKick's SessionState = 1 branch (Outcome = 3, ConflictTearingDown) instead
--     of the intended Outcome = 2 (ConflictGameKicked) branch, permanently breaking the cross-shard
--     kick-and-take-over path for that account.
--
-- Fix: mirror usp_AccountSession_ClearIfOwner's own ownership match exactly -- add @ServerKind/@ShardId/
-- @SessionToken parameters and fold them into the UPDATE's WHERE clause. A connection whose teardown no longer
-- owns the row (already reassigned to a different ServerKind/ShardId/SessionToken by the other side of the
-- race) now silently no-ops here too, exactly as usp_AccountSession_ClearIfOwner already does one statement
-- later in the same sequence -- so a mid-race Login-side teardown can never poison a row the Game side has
-- already legitimately claimed, and neither call in this two-step sequence can ever affect a row it no longer
-- owns. IAccountSessionRepository.MarkTearingDownAsync's signature grows to match ClearIfOwnerAsync's, and both
-- LoginConnectionHost.TearDownAccountSessionAsync and GameConnectionHost's copy now pass the same
-- (ServerKind, ShardId, SessionToken) triple to this call that they already pass to the ClearIfOwnerAsync call
-- immediately after it.
CREATE OR ALTER PROCEDURE runtime.usp_AccountSession_MarkTearingDown @AccountId INT,
                                                                     @ServerKind TINYINT,
                                                                     @ShardId TINYINT NULL,
                                                                     @SessionToken UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    UPDATE runtime.AccountSessions
    SET SessionState = 1
    WHERE AccountId = @AccountId
      AND ServerKind = @ServerKind
      AND (@ShardId IS NULL OR ShardId = @ShardId)
      AND SessionToken = @SessionToken;
END;
GO
